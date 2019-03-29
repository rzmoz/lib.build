﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class SolutionPostBuild
    {
        private readonly BuildArgs _args;

        private static readonly IReadOnlyList<string> _runtimeDirList = new[]
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
            "runtimes",
            "tr",
            "zh-Hans",
            "zh-Hant"
        };

        public SolutionPostBuild(BuildArgs args)
        {
            _args = args;
        }

        public void Run()
        {
            Log.Information($"Starting {nameof(SolutionPostBuild)}");
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
            _runtimeDirList.ForEachParallel(dir => targetDir.ToDir(dir).DeleteIfExists());
        }
        private void CopyArtifacts(FilePath projectFile)
        {
            
            var releaseTargetDir = GetTargetDir(projectFile);
            var configurationDir = GetConfigurationDir(projectFile);
            
            if (_args.Publish)
            {
                Log.Debug($"Publishing {releaseTargetDir.Name}");
                var publishResult= ExternalProcess.Run("dotnet", $" publish \"{projectFile.FullName()}\" --configuration {_args.Configuration} --force --no-build --verbosity quiet --output \"{releaseTargetDir}\"", null, Log.Error);
                if (publishResult.ExitCode != 0)
                    throw new BuildException($"Publish failed for {projectFile.NameWoExtension}. See logs for details");
            }
            else
            {
                var robocopyOutput = new StringBuilder();
                var buildOutputDir = configurationDir.EnumerateDirectories().Single();
                var result = Robocopy.CopyDir(buildOutputDir.FullName(), releaseTargetDir.FullName(), writeOutput: output => robocopyOutput.Append(output), writeError: error => robocopyOutput.Append(error));
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
