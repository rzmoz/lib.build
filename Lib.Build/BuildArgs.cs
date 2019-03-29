using System.Collections.Generic;
using System.IO;
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
                     {"releaseFilter", nameof(ReleaseProjectFilter) },
                     {"testFilter", nameof(TestProjectFilter) },
                 })
                 .Build(args);

            Publish = args.IsSet(nameof(Publish));
            SolutionDir = _cliArgs[nameof(SolutionDir), 0]?.ToDir();
            ReleaseProjectFilter = _cliArgs[nameof(ReleaseProjectFilter)] ?? "*.csproj";
            TestProjectFilter = _cliArgs[nameof(TestProjectFilter)] ?? "*.tests.csproj";

            if (SolutionDir == null) return;
            if (SolutionDir.Exists() || Path.IsPathRooted(SolutionDir.RawPath))
                return;

            var tryInDefaultProjectsDir = "c:\\projects".ToDir().Add(SolutionDir.Segments.ToArray());
            if (tryInDefaultProjectsDir.Exists())
                SolutionDir = tryInDefaultProjectsDir;
        }

        public string Configuration => _cliArgs[nameof(Configuration)] ?? "release";
        public DirPath SolutionDir { get; set; }
        public DirPath ArtifactsDir => _cliArgs[nameof(ArtifactsDir)]?.ToDir() ?? SolutionDir?.ToDir(".releaseArtifacts");

        public bool Publish { get; }

        public SemVersion Version => SemVersion.Parse(_cliArgs[nameof(Version)]);

        public string ReleaseProjectFilter { get; }
        public string TestProjectFilter { get; }

        public IReadOnlyList<FilePath> ReleaseProjects { get; set; } = new List<FilePath>();
        public IReadOnlyList<FilePath> TestProjects { get; set; } = new List<FilePath>();
    }
}