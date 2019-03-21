using System.Linq;
using System.Text;
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
            Log.Information("Starting {Step}", nameof(SolutionPostBuild));
            _args.ArtifactsDir.CreateIfNotExists();
            await _args.ReleaseProjects.ForEachParallelAsync(ProjectPostBuildAsync).ConfigureAwait(false);
        }

        private Task ProjectPostBuildAsync(FilePath projectFile)
        {
            var sourceDir = projectFile.Directory().ToDir("bin", _args.Configuration).EnumerateDirectories().Single();
            var targetDir = _args.ArtifactsDir.ToDir(projectFile.NameWoExtension).FullName();
            Log.Debug($"Copying Artifacts for {projectFile.Name}");

            var robocopyOutput = new StringBuilder();

            var result = Robocopy.CopyDir(sourceDir.FullName(), targetDir, includeSubFolders: true, writeOutput: output => robocopyOutput.Append(output), writeError: error => robocopyOutput.Append(error));
            if (result.Failed)
                throw new BuildException($"Copying Artifacts {projectFile.Name} failed with: {result.ExitCode}|{result.StatusMessage}\r\n{robocopyOutput}");
            return Task.CompletedTask;
        }
    }
}
