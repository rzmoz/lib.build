using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotNet.Basics.Cli;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;
using Microsoft.Extensions.Configuration;

namespace Lib.Build
{
    public class BuildHost : CliHost
    {
        public static IReadOnlyDictionary<string, string> KeyMappings { get; } = new Dictionary<string, string>
        {
            {"v", nameof(Version)},
            {"c", nameof(Configuration)},
            {"p", nameof(Publish)},
            {"config", nameof(Configuration)},
            {"sln", nameof(SolutionDir)},
            {"slnDir", nameof(SolutionDir)},
            {"release", nameof(ReleaseProjectFilter)},
            {"releaseFilter", nameof(ReleaseProjectFilter)},
            {"test", nameof(TestProjectFilter)},
            {"testFilter", nameof(TestProjectFilter)},
            {"preBuild", nameof(PreBuildCallbackFilter)},
            {"preBuildFilter", nameof(PreBuildCallbackFilter)},
            {"postBuild", nameof(PostBuildCallbackFilter)},
            {"postBuildFilter", nameof(PostBuildCallbackFilter)},
        };

        public BuildHost(string[] args, IConfigurationRoot config, ILogDispatcher log) : base(args, config, log)
        {
            Publish = args.IsSet(nameof(Publish));
            SolutionDir = base[nameof(SolutionDir), 0]?.ToDir();
            ReleaseProjectFilter = base[nameof(ReleaseProjectFilter)] ?? "*.csproj";
            TestProjectFilter = base[nameof(TestProjectFilter)] ?? "*.tests.csproj";
            PreBuildCallbackFilter = base[nameof(PreBuildCallbackFilter)] ?? "*.PreBuild.Callback.ps1";
            PostBuildCallbackFilter = base[nameof(PostBuildCallbackFilter)] ?? "*.PostBuild.Callback.ps1";

            if (SolutionDir == null) return;
            if (SolutionDir.Exists() || Path.IsPathRooted(SolutionDir.RawPath))
                return;

            var tryInDefaultProjectsDir = "c:\\projects".ToDir().Add(SolutionDir.Segments.ToArray());
            if (tryInDefaultProjectsDir.Exists())
                SolutionDir = tryInDefaultProjectsDir;
        }

        public string Configuration => base[nameof(Configuration)] ?? "release";
        public DirPath SolutionDir { get; set; }
        public DirPath ArtifactsDir => base[nameof(ArtifactsDir)]?.ToDir() ?? SolutionDir?.ToDir(".releaseArtifacts");

        public bool Publish { get; }

        public SemVersion Version => SemVersion.Parse(base[nameof(Version)]);

        public string ReleaseProjectFilter { get; }
        public string TestProjectFilter { get; }
        public string PreBuildCallbackFilter { get; }
        public string PostBuildCallbackFilter { get; }

        public IReadOnlyList<FilePath> ReleaseProjects { get; set; } = new List<FilePath>();
        public IReadOnlyList<FilePath> TestProjects { get; set; } = new List<FilePath>();
        public IReadOnlyList<FilePath> PreBuildCallbacks { get; set; } = new List<FilePath>();
        public IReadOnlyList<FilePath> PostBuildCallbacks { get; set; } = new List<FilePath>();
    }
}