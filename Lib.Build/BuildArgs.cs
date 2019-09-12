using System.Collections.Generic;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class BuildArgs
    {
        public string Configuration { get; set; } = "release";

        public DirPath SolutionDir { get; set; }
        public DirPath ReleaseArtifactsDir { get; set; }
        public DirPath TestArtifactsDir { get; set; }
        
        /// <summary>
        /// Triggers (dotnet) publish instead of (dotnet) build
        /// </summary>
        public bool Publish { get; set; }
        
        /// <summary>
        /// Ensures output is packaged
        /// </summary>
        public bool Package { get; set; }

        public SemVersion Version { get; set; } = new SemVersion(0, 0, 0);

        public IReadOnlyList<FilePath> ReleaseProjects { get; set; } = new FilePath[0];
        public IReadOnlyList<FilePath> TestProjects { get; set; } = new FilePath[0];

        public IReadOnlyList<FilePath> PreBuildCallbacks { get; set; } = new FilePath[0];
        public IReadOnlyList<FilePath> PostBuildCallbacks { get; set; } = new FilePath[0];
    }
}
