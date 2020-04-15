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
        private static readonly IReadOnlyList<string> _excessivePublishDirs = new[] { "cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant", "Publish" };

        protected override Task<int> InnerRunAsync(BuildArgs args, ILogger log)
        {
            log.Info($"Starting {nameof(SolutionPostBuild)}");
            args.ReleaseProjects.ForEachParallel(proj =>
            {
                var artifactsSourceDir = GetArtifactsSourceDir(proj, args);
                var artifactsTargetDir = args.ReleaseArtifactsDir.Add(proj.NameWoExtension);

                CleanExcessiveCompileArtifacts(artifactsSourceDir, log);

                if (args.IsAuxiliary)
                    RemoveReleaseAssemblies(artifactsSourceDir, proj.NameWoExtension, log);

                if (args.DllsInBinFolderRoles.Any())
                {
                    log.Info($"Asserting Dlls In Bin Folder Roles ");
                    ProcessDllsInBinFolderRoles(artifactsSourceDir, args.DllsInBinFolderRoles, log);
                }

                log.Info($"Asserting Web Jobs");
                AssertWebJob(artifactsSourceDir, proj.NameWoExtension, log);

                log.Info($"Copying Release Artifacts");
                CopyReleaseArtifacts(artifactsSourceDir, artifactsTargetDir, log);
            });

            log.Info($"Scanning for Nuget Packages");
            args.ReleaseProjects.ForEachParallel(proj => CopyNugetPackages(proj, args.ReleaseArtifactsDir));

            if (args.ZipIt)
            {
                log.Info($"Zipping release artifacts");
                ZipIt(args, log);
            }

            return Task.FromResult(0);
        }
        
        private void RemoveReleaseAssemblies(DirPath artifactsSourceDir, string projectName, ILogger log)
        {
            log.Info($"Removing Release Assemblies");

            artifactsSourceDir.EnumerateFiles($"{projectName}.dll", SearchOption.AllDirectories)
                .Concat(artifactsSourceDir.EnumerateFiles($"{projectName}.pdb", SearchOption.AllDirectories))
                .OrderBy(file => file.FullName())
                .ForEach(file =>
                {
                    log.Verbose($"Removing {file.Name.Highlight()}");
                    file.DeleteIfExists();
                });
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

            if (args.Publish)
                sourceDir = sourceDir.Add(args.Runtime, nameof(args.Publish));
            return sourceDir;
        }

        private void CleanExcessiveCompileArtifacts(DirPath artifactsSourceDir, ILogger log)
        {
            _excessivePublishDirs.ForEachParallel(dir =>
            {
                var excessiveDir = artifactsSourceDir.Add(dir);
                log.Verbose($"Cleaning excessive build output dir: {excessiveDir.FullName()}");
                return excessiveDir.DeleteIfExists();
            });
        }

        private void CopyReleaseArtifacts(DirPath artifactsSourceDir, DirPath artifactsTargetDir, ILogger log)
        {
            log.Debug($"Copying RELEASE artifacts for {artifactsTargetDir.Name.Highlight()} from {artifactsSourceDir.FullName()} to {artifactsTargetDir.FullName().Highlight()}");

            try
            {
                artifactsSourceDir.CopyTo(artifactsTargetDir, true);
            }
            catch (Exception e)
            {
                throw new CliException($"Copy BUILD artifacts for {artifactsTargetDir.NameWoExtension} failed with {e.Message}");
            }
        }

        private void AssertWebJob(DirPath artifactsSourceDir, string projectName, ILogger log)
        {
            var appSettingsFile = artifactsSourceDir.ToFile("appSettings.json");

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
            artifactsSourceDir.CopyTo(tempDir.Root, true);
            artifactsSourceDir.CleanIfExists();
            var webJobTargetDir = artifactsSourceDir.Add("app_data", "jobs", webJobType, projectName);
            tempDir.Root.CopyTo(webJobTargetDir, true);
        }

        private void ProcessDllsInBinFolderRoles(DirPath artifactsSourceDir, IReadOnlyList<string> dllsInBinFolderRoles, ILogger log)
        {
            artifactsSourceDir.EnumerateDirectories().ForEachParallel(dir =>
            {
                var roleCandidate = dir.Name.Substring(dir.Name.LastIndexOf(".", StringComparison.Ordinal) + 1);
                if (dllsInBinFolderRoles.Contains(roleCandidate, StringComparer.InvariantCultureIgnoreCase))
                {
                    log.Debug($"Moving dlls to bin folder for {dir.Name.Highlight()}.");
                    var binDir = dir.Add("bin").CreateIfNotExists();
                    dir.EnumerateFiles("*.dll").ForEachParallel(dll => dll.MoveTo(binDir));
                    dir.EnumerateFiles("*.pdb").ForEachParallel(pdb => pdb.MoveTo(binDir));
                }
            });
        }

        private void CopyNugetPackages(FilePath projectFile, DirPath releaseArtifactsDir)
        {
            try
            {
                projectFile.Directory.Add("bin").EnumerateFiles("*.nupkg", SearchOption.AllDirectories)
                    .ForEachParallel(nuget => nuget.CopyTo(releaseArtifactsDir));

            }
            catch (Exception e)
            {
                throw new CliException($"Copy Nuget packages for {projectFile.Name} failed with {e.Message}");
            }
        }

        private void ZipIt(BuildArgs args, ILogger log)
        {
            var sevenZip = new SevenZipExe(log.Debug, log.Error, log.Verbose);

            args.ReleaseArtifactsDir.EnumerateDirectories().ForEachParallel(moduleDir =>
            {
                var runTimesDir = moduleDir.EnumerateDirectories("runtimes").SingleOrDefault();
                var modulePath = args.ReleaseArtifactsDir.ToFile($"{moduleDir.Name}_{args.Version.SemVer20String}");

                log.Debug($"Archive name resolved to: {modulePath}");
                sevenZip.Create7zFromDirectory(moduleDir.FullName(), modulePath.FullName());
                if (runTimesDir == null)
                    return;

                var outputSize = new FileInfo($"{modulePath}.7z").Length;

                if (outputSize <= 0)
                    throw new CliException($"{modulePath} not found. This should never happen. Has the file been deleted?");

                if (outputSize < 10000000)//only create separate tool and runtimes packages when package is more than 10MB (secure file max size on Azure DevOps)
                    return;

                log.Debug($"{modulePath.Name} is larger than 10MB. Splitting runtimes and tool into separate packages");

                sevenZip.Create7zFromDirectory(runTimesDir.FullName(), $"{modulePath.FullName()}_Runtimes");
                runTimesDir.DeleteIfExists();
                sevenZip.Create7zFromDirectory(moduleDir.FullName(), $"{modulePath.FullName()}_Tool");
            });
        }
    }
}
