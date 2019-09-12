using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.SevenZip;
using DotNet.Basics.Sys;
using Newtonsoft.Json.Linq;

namespace Lib.Build
{
    public class SolutionPostBuild
    {
        private readonly BuildArgs _args;

        private readonly ILogDispatcher _slnLog;

        private static readonly IReadOnlyList<string> _winRunTimes = new[] { "win", "win-x64", "win10-x64" };
        private static readonly IReadOnlyList<string> _languageDirs = new[] { "cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant" };

        public SolutionPostBuild(BuildArgs args, ILogDispatcher slnLog)
        {
            _args = args;
            _slnLog = slnLog.InContext(nameof(SolutionPostBuild));
        }

        public void Run()
        {
            _slnLog.Information($"Starting {nameof(SolutionPostBuild)}");
            _args.ReleaseProjects.ForEachParallel(CleanExcessiveCompileArtifacts);
            _slnLog.Information($"Asserting Web Jobs");
            _args.ReleaseProjects.ForEachParallel(AssertWebJob);
            _slnLog.Information($"Copying Release Artifacts");
            _args.ReleaseProjects.ForEachParallel(CopyReleaseArtifacts);

            if (_args.Package)
                PackageReleaseArtifacts();
        }

        private DirPath GetTargetDir(FilePath projectFile)
        {
            return _args.ReleaseArtifactsDir.ToDir(projectFile.NameWoExtension);
        }

        private DirPath GetArtifactsSourceDir(FilePath projectFile)
        {
            var sourceDir = projectFile.Directory().ToDir("bin");
            if (sourceDir.Add(_args.Configuration).Exists())
                sourceDir = sourceDir.Add(_args.Configuration);
            try
            {
                sourceDir = sourceDir.EnumerateDirectories().SingleOrDefault() ?? sourceDir;//look for target framework dir
            }
            catch (InvalidOperationException)
            {
                //ignore if there's more than a single file.
            }

            if (_args.Publish && sourceDir.Add(nameof(_args.Publish)).Exists())
                sourceDir = sourceDir.Add(nameof(_args.Publish));

            return sourceDir;
        }

        private void CleanExcessiveCompileArtifacts(FilePath projectFile)
        {
            var outputDir = GetArtifactsSourceDir(projectFile);
            _languageDirs.ForEachParallel(dir => outputDir.ToDir(dir).DeleteIfExists());
            if (_args.Publish)
                return;
            outputDir.Add(nameof(_args.Publish)).DeleteIfExists();
        }

        private void PackageReleaseArtifacts()
        {
            //scan for nuget packages
            _args.ReleaseProjects.Select(proj => proj.Directory.Add("bin").EnumerateDirectories()).SelectMany(dir => dir).ForEachParallel(dir =>
              {
                  _slnLog.Verbose($"Looking for nuget packages in {dir}");
                  dir.EnumerateFiles("*.nupkg", SearchOption.AllDirectories).ForEachParallel(nuget =>
                  {
                      _slnLog.Debug($"Nuget found: {nuget.FullName()}");
                      nuget.CopyTo(_args.ReleaseArtifactsDir);
                  });
              });

            var sevenZip = new SevenZipExe(_slnLog.Debug, _slnLog.Error);

            _args.ReleaseArtifactsDir.EnumerateDirectories().ForEachParallel(moduleDir =>
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

                var baseName = _args.ReleaseArtifactsDir.ToFile($"{moduleDir.Name}").FullName();
                if (_args.Version > SemVersion.Parse("0.0.0"))
                    baseName += $"_{_args.Version.SemVer10String}";
                sevenZip.Create7zFromDirectory(moduleDir.FullName(), baseName);
                sevenZip.Create7zFromDirectory(runTimes.FullName(), $"{baseName}_runtimes");
                runTimes.DeleteIfExists();
                sevenZip.Create7zFromDirectory(moduleDir.FullName(), $"{baseName}_tool");
            });
        }

        private void CopyReleaseArtifacts(FilePath projectFile)
        {
            var releaseTargetDir = GetTargetDir(projectFile);

            _slnLog.Debug($"Copying build artifacts for {releaseTargetDir.Name.Highlight()} to {releaseTargetDir.FullName()}");

            var artifactsSourceDir = GetArtifactsSourceDir(projectFile);

            try
            {
                artifactsSourceDir.CopyTo(releaseTargetDir, true);
            }
            catch (Exception)
            {
                throw new BuildException($"Copy artifacts for {projectFile.Name} failed with");
            }
        }

        private void AssertWebJob(FilePath projectFile)
        {
            var outputDir = GetArtifactsSourceDir(projectFile);
            var projectName = projectFile.NameWoExtension;


            var appSettingsFile = outputDir.ToFile("appSettings.json");

            if (appSettingsFile.Exists() == false)
            {
                _slnLog.Debug($"{projectName.Highlight()} not WebJob.");
                return;
            }
            var appSettingsRawContent = appSettingsFile.ReadAllText(IfNotExists.Mute).ToLowerInvariant();//lowercase to ignore case when accessing properties

            var appSettingsJson = JToken.Parse(appSettingsRawContent);
            var webJobType = appSettingsJson["webjob"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(webJobType))
            {
                _slnLog.Verbose($"{projectName.Highlight()} not WebJob. Has appsettings file but Property 'webjob' not found\r\n{appSettingsRawContent}");
                return;
            }
            _slnLog.Verbose($"{projectName.Highlight()} is {webJobType} WebJob.");

            using var tempDir = new TempDir();
            //move content to temp dir since we're copying to sub path of origin
            outputDir.CopyTo(tempDir.Root, true);
            outputDir.CleanIfExists();
            var webJobTargetDir = outputDir.Add("app_data", "jobs", webJobType, projectName);
            tempDir.Root.CopyTo(webJobTargetDir, true);
        }
    }
}
