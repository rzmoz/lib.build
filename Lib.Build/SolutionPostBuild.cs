using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DotNet.Basics.Cli;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.SevenZip;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class SolutionPostBuild : BuildStep
    {
        private static readonly IReadOnlyList<string> _winRunTimes = new[] { "win", "win-x64", "win10-x64" };
        private static readonly IReadOnlyList<string> _excessiveBuildDirs = new[] { "cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant", "Publish" };

        protected override Task<int> InnerRunAsync(BuildArgs args, ILogDispatcher log)
        {
            log.Info($"Starting {nameof(SolutionPostBuild)}");
            args.ReleaseProjects.ForEachParallel(proj => CleanExcessiveCompileArtifacts(proj, args, log));
            log.Info($"Asserting Web Jobs");
            args.ReleaseProjects.ForEachParallel(proj => AssertWebJob(proj, args.Configuration, log));
            log.Info($"Copying Release Artifacts");
            args.ReleaseProjects.ForEachParallel(proj => CopyReleaseArtifacts(proj, args.ReleaseArtifactsDir, args.Configuration, log));

            if (args.Package)
                PackageReleaseArtifacts(args, log);
            return Task.FromResult(0);
        }

        private DirPath GetTargetDir(FilePath projectFile, DirPath releaseArtifactsDir)
        {
            return releaseArtifactsDir.ToDir(projectFile.NameWoExtension);
        }

        private DirPath GetArtifactsSourceDir(FilePath projectFile, string configuration)
        {
            var sourceDir = projectFile.Directory().ToDir("bin");
            if (sourceDir.Add(configuration).Exists())
                sourceDir = sourceDir.Add(configuration);
            try
            {
                sourceDir = sourceDir.EnumerateDirectories().SingleOrDefault() ?? sourceDir;//look for target framework dir
            }
            catch (InvalidOperationException)
            {
                //ignore if there's more than a single file.
            }

            return sourceDir;
        }

        private void CleanExcessiveCompileArtifacts(FilePath projectFile, BuildArgs args, ILogDispatcher log)
        {
            var outputDir = GetArtifactsSourceDir(projectFile, args.Configuration);
            _excessiveBuildDirs.ForEachParallel(dir =>
            {
                var excessiveDir = outputDir.Add(dir);
                log.Verbose($"Cleaning excessive build output dir: {excessiveDir.FullName()}");
                return excessiveDir.DeleteIfExists();
            });
        }

        private void PackageReleaseArtifacts(BuildArgs args, ILogDispatcher log)
        {
            log.Info($"{nameof(args.Package)} is set. Packing release artifacts from {args.ReleaseArtifactsDir}");

            //scan for nuget packages
            args.ReleaseProjects.Select(proj => proj.Directory.Add("bin").EnumerateDirectories()).SelectMany(dir => dir).ForEachParallel(dir =>
              {
                  log.Verbose($"Looking for nuget packages in {dir}");
                  dir.EnumerateFiles("*.nupkg", SearchOption.AllDirectories).ForEachParallel(nuget =>
                  {
                      log.Debug($"Nuget found: {nuget.FullName()}");
                      nuget.CopyTo(args.ReleaseArtifactsDir);
                  });
              });

            var sevenZip = new SevenZipExe(log.Debug, log.Error, log.Verbose);

            args.ReleaseArtifactsDir.EnumerateDirectories().ForEachParallel(moduleDir =>
            {
                //cleanup runtime dirs - windows only support
                var runTimesDir = moduleDir.EnumerateDirectories("runtimes").SingleOrDefault();
                runTimesDir?.EnumerateDirectories().ForEachParallel(runtimeDir =>
                {
                    if (_winRunTimes.Contains(runtimeDir.Name, StringComparer.InvariantCultureIgnoreCase))
                        return;
                    runtimeDir.DeleteIfExists();
                });

                var modulePath = args.ReleaseArtifactsDir.ToFile($"{moduleDir.Name}_{args.Version.SemVer20String}");

                log.Debug($"Package name resolved to: {modulePath}");
                sevenZip.Create7zFromDirectory(moduleDir.FullName(), modulePath.FullName());
                if (runTimesDir == null)
                    return;

                var outputSize = new FileInfo($"{modulePath}.7z").Length;

                if (outputSize <= 0)
                    throw new CliException($"{modulePath} not found. This should never happen. Has the file been deleted?", LogOptions.ExcludeStackTrace);

                if (outputSize < 10000000)//only create separate tool and runtimes packages when package is more than 10MB (secure file max size on Azure DevOps)
                    return;

                log.Debug($"{modulePath.Name} is larger than 10MB. Splitting runtimes and tool into separate packages");
                
                sevenZip.Create7zFromDirectory(runTimesDir.FullName(), $"{modulePath.FullName()}_Runtimes");
                runTimesDir.DeleteIfExists();
                sevenZip.Create7zFromDirectory(moduleDir.FullName(), $"{modulePath.FullName()}_Tool");
            });
        }


        private void CopyReleaseArtifacts(FilePath projectFile, DirPath targetOutputRootDir, string buildConfiguration, ILogDispatcher log)
        {
            var releaseTargetDir = GetTargetDir(projectFile, targetOutputRootDir);

            log.Debug($"Copying RELEASE artifacts for {releaseTargetDir.Name.Highlight()} to {releaseTargetDir.FullName()}");

            var artifactsSourceDir = GetArtifactsSourceDir(projectFile, buildConfiguration);

            try
            {
                artifactsSourceDir.CopyTo(releaseTargetDir, true);
            }
            catch (Exception e)
            {
                throw new BuildException($"Copy BUILD artifacts for {projectFile.Name} failed with {e.Message}", 500);
            }
        }

        private void AssertWebJob(FilePath projectFile, string buildConfiguration, ILogDispatcher log)
        {
            var outputDir = GetArtifactsSourceDir(projectFile, buildConfiguration);
            var projectName = projectFile.NameWoExtension;


            var appSettingsFile = outputDir.ToFile("appSettings.json");

            if (appSettingsFile.Exists() == false)
            {
                log.Debug($"{projectName.Highlight()} not WebJob.");
                return;
            }
            var appSettingsRawContent = appSettingsFile.ReadAllText(IfNotExists.Mute).ToLowerInvariant();//lowercase to ignore case when accessing properties

            var appSettingsJsonDoc = JsonDocument.Parse(appSettingsRawContent);
            if (appSettingsJsonDoc.RootElement.TryGetProperty("webjob", out JsonElement webJobElement) == false)
            {
                log.Verbose($"{projectName.Highlight()} not WebJob. Has appsettings file but Property 'webjob' not found\r\n{appSettingsRawContent}");
                return;
            }

            var webJobType = webJobElement.GetString();

            log.Success($"{projectName.Highlight()} is {webJobType} WebJob!");

            using var tempDir = new TempDir();
            //move content to temp dir since we're copying to sub path of origin
            outputDir.CopyTo(tempDir.Root, true);
            outputDir.CleanIfExists();
            var webJobTargetDir = outputDir.Add("app_data", "jobs", webJobType, projectName);
            tempDir.Root.CopyTo(webJobTargetDir, true);
        }
    }
}
