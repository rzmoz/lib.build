using System.Collections.Generic;
using DotNet.Basics.Cli;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class BuildArgs
    {
        private readonly CliArgs _cliArgs;

        private readonly SwitchMappings _switchMappings = new SwitchMappings
        {
            {"c", nameof(Configuration) },
            {"config", nameof(Configuration) },
            {"sln", nameof(SolutionDir) },
            {"slnDir", nameof(SolutionDir) },
            {"ps1",nameof(Ps1CallbackRootDir) },
            {"ps1Dir", nameof(Ps1CallbackRootDir) },
        };

        public BuildArgs(string[] args)
        {
            _cliArgs = new CliArgsBuilder()
                 .WithSerilog()
                 .Build(args, _switchMappings);
        }

        public string Configuration => _cliArgs[nameof(Configuration)] ?? "release";
        public DirPath SolutionDir => _cliArgs[nameof(SolutionDir)]?.ToDir();
        public DirPath Ps1CallbackRootDir => _cliArgs[nameof(Ps1CallbackRootDir)]?.ToDir();
        public DirPath ArtifactsDir => _cliArgs[nameof(ArtifactsDir)]?.ToDir() ?? SolutionDir?.ToDir(".releaseArtifacts");

        public bool IsDebug => _cliArgs.IsDebug;

        public IReadOnlyList<FilePath> PreBuildCallbacks { get; set; } = new List<FilePath>();
        public IReadOnlyList<FilePath> BuildCallbacks { get; set; } = new List<FilePath>();
        public IReadOnlyList<FilePath> PostBuildCallbacks { get; set; } = new List<FilePath>();
    }
}