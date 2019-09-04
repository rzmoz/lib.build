﻿using System;
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

        public async Task RunAsync()
        {
            _slnLog.Information($"Starting {nameof(SolutionPostBuild)}");
            _host.ReleaseProjects.ForEachParallel(CopyArtifacts);
            _host.ReleaseProjects.ForEachParallel(CleanRuntimeArtifacts);

            if (_host.PreBuildCallbacks.Any())
            {
                _slnLog.Information($"Solution PostBuild callbacks found.");
                foreach (var solutionPostBuildCallback in _host.PostBuildCallbacks)
                {
                    _slnLog.Verbose($"Invoking {solutionPostBuildCallback.FullName()}{Environment.NewLine}{solutionPostBuildCallback.ReadAllText()}");
                    await LongRunningOperations.StartAsync(solutionPostBuildCallback.Name, () =>
                        {
                            PowerShellCli.Run(_slnLog, new PowerShellCmdlet($"& \"{solutionPostBuildCallback.FullName()}\"")
                                .WithParam("slnDir", _host.SolutionDir.FullName())
                                .WithParam("artifactsDir", _host.ArtifactsDir.FullName())
                                .WithVerbose()
                            );
                            return Task.CompletedTask;
                        }).ConfigureAwait(false);
                }
            }
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

            projectLog.Debug($"Copying build artifacts for {releaseTargetDir.Name.Highlight()}");
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
