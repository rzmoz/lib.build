using System.Collections.Generic;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class BuildArgs
    {
        public string Configuration { get; set; } = "release";
        public DirPath SolutionDir { get; set; }

        public SemVersion Version { get; set; } = "0.0.0".ToSemVersion();
        public string ReleaseProjectFilter { get; set; } = "*.csproj";
        public string ExcludeProjectFilter { get; set; } = "*.tests.csproj";
        public DirPath ReleaseArtifactsDir { get; set; } = ".releaseArtifacts".ToDir();
        public bool Publish { get; set; }
        public string Runtime { get; set; } = "win-x64";
        public bool ZipIt { get; set; }

        public IReadOnlyList<string> DllsInBinFolderRoles { get; set; } = new string[0];
        public bool IsAuxiliary { get; set; }
        public IReadOnlyList<FilePath> ReleaseProjects { get; set; } = new FilePath[0];
    }
}
