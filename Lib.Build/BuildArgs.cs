using System.Collections.Generic;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class BuildArgs
    {
        public string Configuration { get; set; } = "release";
        public DirPath SolutionDir { get; set; }
        public SemVersion Version { get; set; } = new SemVersion(0, 0, 0);

        public DirPath ReleaseArtifactsDir { get; set; }
        public bool Package { get; set; }
        
        public string TestFilter { get; set; }
        public bool WithTestResults { get; set; }
        public DirPath TestResultsDir { get; set; }
        
        public IReadOnlyList<FilePath> TestProjects { get; set; } = new FilePath[0];
        public IReadOnlyList<FilePath> ReleaseProjects { get; set; } = new FilePath[0];
    }
}
