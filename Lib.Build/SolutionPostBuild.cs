using System.Collections.Generic;
using DotNet.Basics.Collections;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;
using Serilog;

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
            Log.Information("Starting {Step}", nameof(SolutionPostBuild));
            _args.ArtifactsDir.CreateIfNotExists();
            _args.ReleaseProjects.ForEachParallel(PublishProjects);
            _args.ReleaseProjects.ForEachParallel(CleanRuntimeArtifacts);
        }

        private DirPath GetTargetDir(FilePath projectFile)
        {
            return _args.ArtifactsDir.ToDir(projectFile.NameWoExtension);
        }

        private void CleanRuntimeArtifacts(FilePath projectFile)
        {
            var targetDir = GetTargetDir(projectFile);
            _runtimeDirList.ForEachParallel(dir => targetDir.ToDir(dir).DeleteIfExists());
        }
        private void PublishProjects(FilePath projectFile)
        {
            var targetDir = GetTargetDir(projectFile);
            Log.Debug($"Publishing {targetDir.Name}");

            var result = ExternalProcess.Run("dotnet", $" publish \"{projectFile.FullName()}\" --configuration {_args.Configuration} --force --no-build --verbosity quiet --output \"{targetDir}\"", null, Log.Error);
            if (result.ExitCode != 0)
                throw new BuildException($"Publish failed for {projectFile.NameWoExtension}. See logs for details");
        }
    }
}
