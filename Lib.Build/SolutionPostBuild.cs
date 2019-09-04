using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly BuildHost _host;

        private const string _dotNetFrameworkPattern = @"^net[0-9]+$";
        private static readonly Regex _dotNetFrameworkRegex = new Regex(_dotNetFrameworkPattern, RegexOptions.IgnoreCase);
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

        public SolutionPostBuild(BuildHost host, ILogDispatcher slnLog)
        {
            _host = host;
            _slnLog = slnLog.InContext(nameof(SolutionPostBuild));
        }

        public void Run()
        {
            _slnLog.Information($"Starting {nameof(SolutionPostBuild)}");
            _host.ArtifactsDir.CreateIfNotExists();
            _host.ReleaseProjects.ForEachParallel(CopyArtifacts);
            _host.ReleaseProjects.ForEachParallel(CleanRuntimeArtifacts);
            _host.ReleaseProjects.ForEachParallel(CopyNugetPackages);
        }

        private DirPath GetTargetDir(FilePath projectFile)
        {
            return _host.ArtifactsDir.ToDir(projectFile.NameWoExtension);
        }

        private DirPath GetConfigurationDir(FilePath projectFile)
        {
            return projectFile.Directory().ToDir("bin", _host.Configuration);
        }

        private DirPath GetArtifactsSourceDir(FilePath projectFile)
        {
            var configurationDir = GetConfigurationDir(projectFile);
            var projectOutputDir = configurationDir.EnumerateDirectories().Single();
            var publishDir = projectOutputDir.Add("publish");
            return publishDir.Exists() ? publishDir : projectOutputDir;
        }

        private void CleanRuntimeArtifacts(FilePath projectFile)
        {
            var targetDir = GetTargetDir(projectFile);
            _languageDirs.ForEachParallel(dir => targetDir.ToDir(dir).DeleteIfExists());
        }

        private void CopyArtifacts(FilePath projectFile)
        {
            var releaseTargetDir = GetTargetDir(projectFile);
            var projectLog = _slnLog.InContext(projectFile.NameWoExtension);

            projectLog.Debug($"Copying build artifacts for {releaseTargetDir.Name}");
            var robocopyOutput = new StringBuilder();
            var artifactsSourceDir = GetArtifactsSourceDir(projectFile);

            if (_dotNetFrameworkRegex.IsMatch(artifactsSourceDir.Name))
            {
                projectLog.Debug($"{projectFile.NameWoExtension} is .NET Framework");
                var binDir = artifactsSourceDir.Add("bin");
                Robocopy.MoveContent(artifactsSourceDir.FullName(), binDir.FullName(), "*.dll", writeOutput: output => robocopyOutput.Append(output), writeError: error => robocopyOutput.Append(error));
                Robocopy.MoveContent(artifactsSourceDir.FullName(), binDir.FullName(), "*.pdb", writeOutput: output => robocopyOutput.Append(output), writeError: error => robocopyOutput.Append(error));
            }

            AssertWebJob(projectFile.NameWoExtension, artifactsSourceDir, projectLog);

            var result = Robocopy.CopyDir(artifactsSourceDir.FullName(), releaseTargetDir.FullName(), includeSubFolders: true, writeOutput: output => robocopyOutput.Append(output), writeError: error => robocopyOutput.Append(error));
            if (result.Failed)
                throw new BuildException($"Copy artifacts for {projectFile.Name} failed with: {result.ExitCode}|{result.StatusMessage}\r\n{robocopyOutput}");

        }

        private void AssertWebJob(string projectName, DirPath outputDir, ILogDispatcher log)
        {
            var appSettingsFile = outputDir.ToFile("appSettings.json");
            log.Debug($"Asserting if WebJob. Looking for {appSettingsFile.FullName()}");
            if (appSettingsFile.Exists() == false)
            {
                log.Debug($"Not WebJob. {appSettingsFile.FullName()} not found");
                return;
            }
            var appSettingsRawContent = appSettingsFile.ReadAllText(IfNotExists.Mute).ToLowerInvariant();//lowercase to ignore case when accessing properties
            log.Debug($"{appSettingsFile.Name} found:\r\n{appSettingsRawContent}");

            var appSettingsJson = JToken.Parse(appSettingsRawContent);
            var webJob = appSettingsJson["webjob"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(webJob))
            {
                log.Debug($"Not WebJob. Property 'webjob' not found in {appSettingsFile.FullName()}");
                return;
            }
            log.Information($"WebJob found: {webJob}");

            var webJobTargetDir = outputDir.Add("app_data", "jobs", webJob, projectName);
            webJobTargetDir.DeleteIfExists();//must be deleted before we scan for dirs
            var webJobFiles = outputDir.EnumerateFiles().ToList();
            var webJobDirs = outputDir.EnumerateDirectories().ToList();//must iterated before we scan for dirs
            webJobTargetDir.CreateIfNotExists();
            webJobFiles.ForEachParallel(f =>
            {
                var targetFile = webJobTargetDir.ToFile(f.Name);
                log.Debug($"File: Moving {f.FullName()} to {targetFile.FullName() }");
                return f.MoveTo(targetFile);
            });
            webJobDirs.ForEachParallel(dir =>
            {
                log.Debug($"Dir: Moving {dir.FullName()} to {webJobTargetDir.FullName()}");
                dir.CopyTo(webJobTargetDir);
                dir.DeleteIfExists();
            });
        }

        private void CopyNugetPackages(FilePath projectFile)
        {
            //check for nuget packages
            var robocopyOutput = new StringBuilder();
            var configurationDir = GetConfigurationDir(projectFile);
            var result = Robocopy.CopyFile(configurationDir.FullName(), _host.ArtifactsDir.FullName(), "*.nupkg", writeOutput: output => robocopyOutput.Append(output), writeError: error => robocopyOutput.Append(error));
            if (result.Failed)
                throw new BuildException($"Copy nupkg packages {projectFile.Name} failed with: {result.ExitCode}|{result.StatusMessage}\r\n{robocopyOutput}");
        }
    }
}
