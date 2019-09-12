using System.Collections.Generic;
using System.Linq;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class BuildArgsDto
    {
        public string Configuration { get; set; }

        public string SolutionDir { get; set; }
        public string ReleaseArtifactsDir { get; set; }
        public string TestArtifactsDir { get; set; }

        public bool Publish { get; set; }

        public bool Package { get; set; }

        public string Version { get; set; }

        public IReadOnlyList<string> ReleaseProjects { get; set; }
        public IReadOnlyList<string> TestProjects { get; set; }

        public IReadOnlyList<string> PreBuildCallbacks { get; set; }
        public IReadOnlyList<string> PostBuildCallbacks { get; set; }

        public static BuildArgsDto CreateDto(BuildArgs args)
        {
            return new BuildArgsDto
            {
                Configuration = args.Configuration,
                SolutionDir = args.SolutionDir.RawPath,
                ReleaseArtifactsDir = args.ReleaseArtifactsDir.RawPath,
                TestArtifactsDir = args.TestArtifactsDir.RawPath,
                Publish = args.Publish,
                Package = args.Package,
                Version = args.Version.SemVer20String,
                ReleaseProjects = args.ReleaseProjects.Select(fp => fp.RawPath).ToList(),
                TestProjects = args.TestProjects.Select(fp => fp.RawPath).ToList(),
                PreBuildCallbacks = args.PreBuildCallbacks.Select(fp => fp.RawPath).ToList(),
                PostBuildCallbacks = args.PostBuildCallbacks.Select(fp => fp.RawPath).ToList(),
            };
        }
        public BuildArgs ToArgs()
        {
            return new BuildArgs
            {
                Configuration = Configuration,
                SolutionDir = SolutionDir.ToDir(),
                ReleaseArtifactsDir = ReleaseArtifactsDir.ToDir(),
                TestArtifactsDir = TestArtifactsDir.ToDir(),
                Publish = Publish,
                Package = Package,
                Version = SemVersion.Parse(Version),
                ReleaseProjects = ReleaseProjects.Select(fp => fp.ToFile()).ToList(),
                TestProjects = TestProjects.Select(fp => fp.ToFile()).ToList(),
                PreBuildCallbacks = PreBuildCallbacks.Select(fp => fp.ToFile()).ToList(),
                PostBuildCallbacks = PostBuildCallbacks.Select(fp => fp.ToFile()).ToList(),
            };
        }
    }
}
