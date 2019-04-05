using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class SolutionPostBuild
    {
        private readonly BuildArgs _args;

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

        public SolutionPostBuild(BuildArgs args, ILogDispatcher slnLog)
        {
            _args = args;
            _slnLog = slnLog.InContext(nameof(SolutionPostBuild));
        }

        public void Run()
        {
            _slnLog.Information($"Starting {nameof(SolutionPostBuild)}");
            _args.ArtifactsDir.CreateIfNotExists();
            _args.ReleaseProjects.ForEachParallel(CopyArtifacts);
            _args.ReleaseProjects.ForEachParallel(CleanRuntimeArtifacts);
            _args.ReleaseProjects.ForEachParallel(CopyNugetPackages);
        }

        private DirPath GetTargetDir(FilePath projectFile)
        {
            return _args.ArtifactsDir.ToDir(projectFile.NameWoExtension);
        }
        private DirPath GetConfigurationDir(FilePath projectFile)
        {
            return projectFile.Directory().ToDir("bin", _args.Configuration); ;
        }

        private void CleanRuntimeArtifacts(FilePath projectFile)
        {
            var targetDir = GetTargetDir(projectFile);
            _languageDirs.ForEachParallel(dir => targetDir.ToDir(dir).DeleteIfExists());
        }
        private void CopyArtifacts(FilePath projectFile)
        {

            var releaseTargetDir = GetTargetDir(projectFile);
            var configurationDir = GetConfigurationDir(projectFile);
            var projectLog = _slnLog.InContext(projectFile.NameWoExtension);

            if (_args.Publish)
            {
                projectLog.Debug($"Publishing {releaseTargetDir.Name}");
                var publishResult = ExternalProcess.Run("dotnet", $" publish \"{projectFile.FullName()}\" --configuration {_args.Configuration} --force --no-build --verbosity quiet --output \"{releaseTargetDir}\"", projectLog.Verbose, projectLog.Error);
                if (publishResult.ExitCode != 0)
                    throw new BuildException($"Publish failed for {projectFile.NameWoExtension}. See logs for details");
            }
            else
            {
                projectLog.Debug($"Copying build artifacts for {releaseTargetDir.Name}");
                var robocopyOutput = new StringBuilder();
                var buildOutputDir = configurationDir.EnumerateDirectories().Single();

                if (_dotNetFrameworkRegex.IsMatch(buildOutputDir.Name))
                {
                    projectLog.Debug($"{projectFile.NameWoExtension} is .NET Framework");
                    var binDir = buildOutputDir.Add("bin");
                    Robocopy.MoveContent(buildOutputDir.FullName(), binDir.FullName(), "*.dll", writeOutput: output => robocopyOutput.Append(output), writeError: error => robocopyOutput.Append(error));
                    Robocopy.MoveContent(buildOutputDir.FullName(), binDir.FullName(), "*.pdb", writeOutput: output => robocopyOutput.Append(output), writeError: error => robocopyOutput.Append(error));
                }

                var result = Robocopy.CopyDir(buildOutputDir.FullName(), releaseTargetDir.FullName(), includeSubFolders: true, writeOutput: output => robocopyOutput.Append(output), writeError: error => robocopyOutput.Append(error));
                if (result.Failed)
                    throw new BuildException($"Copy artifacts for {projectFile.Name} failed with: {result.ExitCode}|{result.StatusMessage}\r\n{robocopyOutput}");
            }
        }
        private void CopyNugetPackages(FilePath projectFile)
        {
            //check for nuget packages
            var robocopyOutput = new StringBuilder();
            var configurationDir = GetConfigurationDir(projectFile);
            var result = Robocopy.CopyFile(configurationDir.FullName(), _args.ArtifactsDir.FullName(), "*.nupkg", writeOutput: output => robocopyOutput.Append(output), writeError: error => robocopyOutput.Append(error));
            if (result.Failed)
                throw new BuildException($"Copy nupkg packages {projectFile.Name} failed with: {result.ExitCode}|{result.StatusMessage}\r\n{robocopyOutput}");
        }
    }
}
