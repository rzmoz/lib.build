using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;
using Serilog;

namespace Lib.Build
{
    public class SolutionPreBuild
    {
        private readonly BuildArgs _args;

        public SolutionPreBuild(BuildArgs args)
        {
            _args = args;
        }

        public async Task RunAsync()
        {
            var cleanupTasks = new List<Task> { CleanDir(_args.ArtifactsDir) };//add artifacts dir
            //add csproj bin dirs 
            var csprojBinDirs = _args.SolutionDir.EnumerateDirectories("*bin*", SearchOption.AllDirectories);
            cleanupTasks.AddRange(csprojBinDirs.Select(dir => CleanDir(dir.ToString().ToDir())));

            await Task.WhenAll(cleanupTasks).ConfigureAwait(false);
        }

        private Task CleanDir(DirPath dir)
        {
            Log.Information($"Cleaning {{Dir}}", dir.FullName());

            if (dir.Exists() == false)
                throw new DirectoryNotFoundException(dir.FullName());

            dir.CleanIfExists();

            return Task.CompletedTask;
        }
    }
}
