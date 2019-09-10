using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;
using Newtonsoft.Json.Linq;

namespace Lib.Build
{
    public class SolutionPostBuild
    {
        private readonly BuildArgs _args;

        private const string _dotNetFrameworkPattern = @"^net[0-9]+$";
        private const string _dotNetStandardPattern = @"^netstandard[0-9\.]+$";
        private const string _dotNetCoreAppPattern = @"^netcoreapp[0-9\.]+$";
        private static readonly Regex _dotNetFrameworkRegex = new Regex(_dotNetFrameworkPattern, RegexOptions.IgnoreCase);
        private static readonly Regex _dotNetStandardRegex = new Regex(_dotNetStandardPattern, RegexOptions.IgnoreCase);
        private static readonly Regex _dotNetCoreAppRegex = new Regex(_dotNetCoreAppPattern, RegexOptions.IgnoreCase);
        private readonly ILogDispatcher _slnLog;

        private static readonly IReadOnlyList<string> _languageDirs = new[]
        {
            "cs",
            "de",
            "es",
            "fr",
            "it",
            "ja",
            "ko",
            "pl",
            "pt-BR",
            "ru",
            "tr",
            "zh-Hans",
            "zh-Hant"
        };

        public SolutionPostBuild(BuildArgs args, ILogDispatcher slnLog)
        {
            _args = args;
            _slnLog = slnLog.InContext(nameof(SolutionPostBuild));
        }

        public void Run()
        {
            _slnLog.Information($"Starting {nameof(SolutionPostBuild)}");
            _slnLog.Information($"Asserting Target Frameworks");
            _args.ReleaseProjects.ForEachParallel(AssertDotNetFrameworkOutput);
            _args.ReleaseProjects.ForEachParallel(CleanLanguageBuildArtifacts);
            _slnLog.Information($"Asserting Web Jobs");
            _args.ReleaseProjects.ForEachParallel(AssertWebJob);
            _slnLog.Information($"Copying Release Artifacts");
            _args.ReleaseProjects.ForEachParallel(CopyReleaseArtifacts);
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

        private void CleanLanguageBuildArtifacts(FilePath projectFile)
        {
            var outputDir = GetArtifactsSourceDir(projectFile);
            _languageDirs.ForEachParallel(dir => outputDir.ToDir(dir).DeleteIfExists());
        }

        private void AssertDotNetFrameworkOutput(FilePath projectFile)
        {
            var artifactsSourceDir = GetArtifactsSourceDir(projectFile);
            var targetFramework = $"Not detected: {artifactsSourceDir.Name}";

            if (_dotNetFrameworkRegex.IsMatch(artifactsSourceDir.Name))
            {
                targetFramework = ".NET Framework";
                var binDir = artifactsSourceDir.Add("bin");

                artifactsSourceDir.EnumerateFiles("*.dll").MoveTo(binDir, true, true);
                artifactsSourceDir.EnumerateFiles("*.pdb").MoveTo(binDir, true, true);
            }
            else if (_dotNetStandardRegex.IsMatch(artifactsSourceDir.Name))
                targetFramework = ".NET Framework";
            else if (_dotNetCoreAppRegex.IsMatch(artifactsSourceDir.Name))
                targetFramework = ".NET Core App";

            _slnLog.Debug($"Target Framework for {projectFile.NameWoExtension.Highlight()} is {targetFramework}");
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

            var log = _slnLog.InContext(projectName);
            var appSettingsFile = outputDir.ToFile("appSettings.json");

            if (appSettingsFile.Exists() == false)
            {
                log.Verbose($"Not WebJob. {appSettingsFile.FullName()} not found");
                return;
            }
            var appSettingsRawContent = appSettingsFile.ReadAllText(IfNotExists.Mute).ToLowerInvariant();//lowercase to ignore case when accessing properties
            log.Debug($"{appSettingsFile.Name} found:\r\n{appSettingsRawContent}");

            var appSettingsJson = JToken.Parse(appSettingsRawContent);
            var webJob = appSettingsJson["webjob"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(webJob))
            {
                log.Verbose($"Not WebJob. Property 'webjob' not found in {appSettingsFile.FullName()}");
                return;
            }
            log.Information($"WebJob found: {webJob}");

            using var tempDir = new TempDir();
            //move content to temp dir since we're copying to sub path of origin
            outputDir.CopyTo(tempDir.Root, true, _slnLog);
            outputDir.CleanIfExists();
            var webJobTargetDir = outputDir.Add("app_data", "jobs", webJob, projectName);
            tempDir.Root.CopyTo(webJobTargetDir, true, _slnLog);
        }
    }
}
