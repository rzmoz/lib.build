using System.Collections.Generic;
using System.Linq;
using DotNet.Basics.Cli;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class BuildArgs
    {
        private readonly CliArgs _cliArgs;

        public BuildArgs(string[] args)
        {
            _cliArgs = new CliArgsBuilder()
                 .WithSerilog()
                 .WithSwitchMappings(() => new SwitchMappings
                 {
                     {"v", nameof(Version) },
                     {"c", nameof(Configuration) },
                     {"config", nameof(Configuration) },
                     {"sln", nameof(SolutionDir) },
                     {"slnDir", nameof(SolutionDir) },
                     {"ps1",nameof(Ps1CallbackRootDir) },
                     {"ps1Dir", nameof(Ps1CallbackRootDir) },
                     {"releaseFilter", nameof(ReleaseProjectFilter) },
                     {"testFilter", nameof(TestProjectFilter) },
                 })
                 .Build(args);

            SolutionDir = _cliArgs[nameof(SolutionDir)]?.ToDir();
            if (SolutionDir == null)
            {
                var tryDir = args.FirstOrDefault();
                if (tryDir == null)
                    return;
                if (tryDir.ToDir().Exists())
                    SolutionDir = tryDir.ToDir();
            }
        }

        public string Configuration => _cliArgs[nameof(Configuration)] ?? "release";
        public DirPath SolutionDir { get; set; }
        public DirPath Ps1CallbackRootDir => _cliArgs[nameof(Ps1CallbackRootDir)]?.ToDir();
        public DirPath ArtifactsDir => _cliArgs[nameof(ArtifactsDir)]?.ToDir() ?? SolutionDir?.ToDir(".releaseArtifacts");

        public SemVersion Version => SemVersion.Parse(_cliArgs[nameof(Version)]);

        public string ReleaseProjectFilter { get; set; } = "*.csproj";
        public string TestProjectFilter { get; set; } = "*.tests.csproj";

        public IReadOnlyList<FilePath> ReleaseProjects { get; set; } = new List<FilePath>();
        public IReadOnlyList<FilePath> TestProjects { get; set; } = new List<FilePath>();

        public IReadOnlyList<FilePath> PreBuildCallbacks { get; set; } = new List<FilePath>();
        public IReadOnlyList<FilePath> BuildCallbacks { get; set; } = new List<FilePath>();
        public IReadOnlyList<FilePath> PostBuildCallbacks { get; set; } = new List<FilePath>();
    }
}