using System.IO;
using DotNet.Basics.Collections;
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

        public void Run()
        {
            Log.Information("Starting {Step}", nameof(SolutionPreBuild));

            CleanDir(_args.ArtifactsDir);
            //add csproj bin dirs 
            var csprojBinDirs = _args.SolutionDir.EnumerateDirectories("*bin*", SearchOption.AllDirectories);
            csprojBinDirs.ForEachParallel(CleanDir);
        }

        private void CleanDir(DirPath dir)
        {
            Log.Debug($"Cleaning {{Dir}}", dir.FullName());
            dir.CleanIfExists();
        }
    }
}
