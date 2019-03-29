using System.IO;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class SolutionPreBuild
    {
        private readonly BuildArgs _args;

        public SolutionPreBuild(BuildArgs args)
        {
            _args = args;
        }

        public void Run()
        {
            Log.Information($"Starting {nameof(SolutionPreBuild)}");

            CleanDir(_args.ArtifactsDir);
            //add csproj bin dirs 
            var csprojBinDirs = _args.SolutionDir.EnumerateDirectories("*bin*", SearchOption.AllDirectories);
            csprojBinDirs.ForEachParallel(CleanDir);
        }

        private void CleanDir(DirPath dir)
        {
            Log.Debug($"Cleaning {dir.FullName()}");
            dir.CleanIfExists();
        }
    }
}
