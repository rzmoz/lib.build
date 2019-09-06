using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.PowerShell;
using DotNet.Basics.Sys;
using Newtonsoft.Json.Linq;

namespace Lib.Build
{
    public class SolutionPostBuild
    {
        private readonly BuildArgs _args;
        private readonly CallbackRunner _callbackRunner;

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

        public SolutionPostBuild(BuildArgs args, ILogDispatcher slnLog, CallbackRunner callbackRunner)
        {
            _args = args;
            _callbackRunner = callbackRunner;
            _slnLog = slnLog.InContext(nameof(SolutionPostBuild));
        }

        public async Task RunAsync()
        {
            _slnLog.Information($"Starting {nameof(SolutionPostBuild)}");
            _args.ReleaseProjects.ForEachParallel(CopyArtifacts);
            _args.ReleaseProjects.ForEachParallel(CleanRuntimeArtifacts);

            await _callbackRunner
                .InvokeCallbacksAsync(_args.PostBuildCallbacks, _args.SolutionDir, _args.ReleaseArtifactsDir, _slnLog)
                .ConfigureAwait(false);
        }

        private DirPath GetTargetDir(FilePath projectFile)
        {
            return _args.ReleaseArtifactsDir.ToDir(projectFile.NameWoExtension);
        }

        private DirPath GetConfigurationDir(FilePath projectFile)
        {
            return projectFile.Directory().ToDir("bin", _args.Configuration);
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


            _slnLog.Debug($"Copying build artifacts for {releaseTargetDir.Name.Highlight()} to {releaseTargetDir.FullName()}");
            var robocopyOutput = new StringBuilder();
            var artifactsSourceDir = GetArtifactsSourceDir(projectFile);

            var projectLog = _slnLog.InContext(projectFile.NameWoExtension);

            if (_dotNetFrameworkRegex.IsMatch(artifactsSourceDir.Name))
            {
                _slnLog.Debug($"{projectFile.NameWoExtension.Highlight()} is .NET Framework");
                var binDir = artifactsSourceDir.Add("bin");
                Robocopy.MoveContent(artifactsSourceDir.FullName(), binDir.FullName(), "*.dll", writeOutput: output => robocopyOutput.Append(output), writeError: error => robocopyOutput.Append(error));
                Robocopy.MoveContent(artifactsSourceDir.FullName(), binDir.FullName(), "*.pdb", writeOutput: output => robocopyOutput.Append(output), writeError: error => robocopyOutput.Append(error));
            }
            else
                _slnLog.Debug($"{projectFile.NameWoExtension.Highlight()} is .NET Core");

            AssertWebJob(projectFile.NameWoExtension, artifactsSourceDir, projectLog);

            var result = Robocopy.CopyDir(artifactsSourceDir.FullName(), releaseTargetDir.FullName(), includeSubFolders: true, writeOutput: output => robocopyOutput.Append(output), writeError: error => robocopyOutput.Append(error));
            if (result.Failed)
                throw new BuildException($"Copy artifacts for {projectFile.Name} failed with: {result.ExitCode}|{result.StatusMessage}\r\n{robocopyOutput}");

        }

        private void AssertWebJob(string projectName, DirPath outputDir, ILogDispatcher log)
        {
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
    }
}
