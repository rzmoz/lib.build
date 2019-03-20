using System.Linq;
using System.Threading.Tasks;
using DotNet.Basics.Collections;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;
using Serilog;

namespace Lib.Build
{
    public class SolutionPostBuild
    {
        private readonly BuildArgs _args;

        public SolutionPostBuild(BuildArgs args)
        {
            _args = args;
        }

        public async Task RunAsync()
        {
            _args.ArtifactsDir.CreateIfNotExists();
            await _args.ReleaseProjects.ForEachParallelAsync(ProjectPostBuildAsync).ConfigureAwait(false);
        }

        private Task ProjectPostBuildAsync(FilePath projectFile)
        {
            var outDir = projectFile.Directory().ToDir("bin", _args.Configuration).EnumerateDirectories().Single();
            Log.Debug($"Copying Release artifacts for {projectFile.Directory().FullName()}");
            var result = Robocopy.CopyDir(outDir.FullName(), _args.ArtifactsDir.ToDir(projectFile.NameWoExtension).FullName(), includeSubFolders: true);
            if (result.Failed)
                throw new BuildException($"Error when copying release artifacts for {projectFile.Name}. See log for details");
            return Task.CompletedTask;
        }
    }
}
