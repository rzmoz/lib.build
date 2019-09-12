using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.SevenZip;
using DotNet.Basics.Sys;
using Newtonsoft.Json.Linq;

namespace Lib.Build
{
    public class SolutionPostBuild : BuildStep
    {
        private static readonly IReadOnlyList<string> _winRunTimes = new[] { "win", "win-x64", "win10-x64" };
        private static readonly IReadOnlyList<string> _languageDirs = new[] { "cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant" };

        protected override Task<int> InnerRunAsync(BuildArgs args, ILogDispatcher log)
        {
            log.Info($"Starting {nameof(SolutionPostBuild)}");
            args.ReleaseProjects.ForEachParallel(proj => CleanExcessiveCompileArtifacts(proj, args, log));
            log.Info($"Asserting Web Jobs");
            args.ReleaseProjects.ForEachParallel(proj => AssertWebJob(proj, args, log));
            log.Info($"Copying Release Artifacts");
            args.ReleaseProjects.ForEachParallel(proj => CopyReleaseArtifacts(proj, args, log));

            if (args.Package)
                PackageReleaseArtifacts(args, log);
            return Task.FromResult(0);
        }

        private DirPath GetTargetDir(FilePath projectFile, DirPath releaseArtifactsDir)
        {
            return releaseArtifactsDir.ToDir(projectFile.NameWoExtension);
        }

        private DirPath GetArtifactsSourceDir(FilePath projectFile, BuildArgs args)
        {
            var sourceDir = projectFile.Directory().ToDir("bin");
            if (sourceDir.Add(args.Configuration).Exists())
                sourceDir = sourceDir.Add(args.Configuration);
            try
            {
                sourceDir = sourceDir.EnumerateDirectories().SingleOrDefault() ?? sourceDir;//look for target framework dir
            }
            catch (InvalidOperationException)
            {
                //ignore if there's more than a single file.
            }

            if (args.Publish && sourceDir.Add(nameof(args.Publish)).Exists())
                sourceDir = sourceDir.Add(nameof(args.Publish));

            return sourceDir;
        }

        private void CleanExcessiveCompileArtifacts(FilePath projectFile, BuildArgs args, ILogDispatcher log)
        {
            var outputDir = GetArtifactsSourceDir(projectFile, args);
            _languageDirs.ForEachParallel(dir => outputDir.ToDir(dir).DeleteIfExists());
            if (args.Publish)
                return;

            outputDir.Add(nameof(args.Publish)).DeleteIfExists();
        }

        private void PackageReleaseArtifacts(BuildArgs args, ILogDispatcher log)
        {
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

            var sevenZip = new SevenZipExe(log.Debug, log.Error);

            args.ReleaseArtifactsDir.EnumerateDirectories().ForEachParallel(moduleDir =>
            {
                var runTimes = moduleDir.EnumerateDirectories("runtimes").SingleOrDefault();
                if (runTimes == null)
                    return;

                runTimes.EnumerateDirectories().ForEachParallel(runtimeDir =>
                {
                    if (_winRunTimes.Contains(runtimeDir.Name, StringComparer.InvariantCultureIgnoreCase))
                        return;
                    runtimeDir.DeleteIfExists();
                });

                var baseName = args.ReleaseArtifactsDir.ToFile($"{moduleDir.Name}").FullName();
                if (args.Version > SemVersion.Parse("0.0.0"))
                    baseName += $"_{args.Version.SemVer10String}";
                sevenZip.Create7zFromDirectory(moduleDir.FullName(), baseName);
                sevenZip.Create7zFromDirectory(runTimes.FullName(), $"{baseName}_runtimes");
                runTimes.DeleteIfExists();
                sevenZip.Create7zFromDirectory(moduleDir.FullName(), $"{baseName}_tool");
            });
        }

        private void CopyReleaseArtifacts(FilePath projectFile, BuildArgs args, ILogDispatcher log)
        {
            var releaseTargetDir = GetTargetDir(projectFile, args.ReleaseArtifactsDir);

            log.Debug($"Copying build artifacts for {releaseTargetDir.Name.Highlight()} to {releaseTargetDir.FullName()}");

            var artifactsSourceDir = GetArtifactsSourceDir(projectFile, args);

            try
            {
                artifactsSourceDir.CopyTo(releaseTargetDir, true);
            }
            catch (Exception e)
            {
                throw new BuildException($"Copy artifacts for {projectFile.Name} failed with {e.Message}", 500);
            }
        }

        private void AssertWebJob(FilePath projectFile, BuildArgs args, ILogDispatcher log)
        {
            var outputDir = GetArtifactsSourceDir(projectFile, args);
            var projectName = projectFile.NameWoExtension;


            var appSettingsFile = outputDir.ToFile("appSettings.json");

            if (appSettingsFile.Exists() == false)
            {
                log.Debug($"{projectName.Highlight()} not WebJob.");
                return;
            }
            var appSettingsRawContent = appSettingsFile.ReadAllText(IfNotExists.Mute).ToLowerInvariant();//lowercase to ignore case when accessing properties

            var appSettingsJson = JToken.Parse(appSettingsRawContent);
            var webJobType = appSettingsJson["webjob"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(webJobType))
            {
                log.Verbose($"{projectName.Highlight()} not WebJob. Has appsettings file but Property 'webjob' not found\r\n{appSettingsRawContent}");
                return;
            }
            log.Verbose($"{projectName.Highlight()} is {webJobType} WebJob.");

            using var tempDir = new TempDir();
            //move content to temp dir since we're copying to sub path of origin
            outputDir.CopyTo(tempDir.Root, true);
            outputDir.CleanIfExists();
            var webJobTargetDir = outputDir.Add("app_data", "jobs", webJobType, projectName);
            tempDir.Root.CopyTo(webJobTargetDir, true);
        }
    }
}
