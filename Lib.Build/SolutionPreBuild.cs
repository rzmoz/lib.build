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
        private readonly ILogDispatcher _slnLog;

        public SolutionPreBuild(BuildArgs args, ILogDispatcher slnLog)
        {
            _args = args;
            _slnLog = slnLog.InContext(nameof(SolutionPreBuild));
        }

        public void Run()
        {
            _slnLog.Information($"Starting {nameof(SolutionPreBuild)}");

            CleanDir(_args.ArtifactsDir);
            //add csproj bin dirs 
            var csprojBinDirs = _args.SolutionDir.EnumerateDirectories("*bin*", SearchOption.AllDirectories);
            csprojBinDirs.ForEachParallel(CleanDir);
        }

        private void CleanDir(DirPath dir)
        {
            _slnLog.Debug($"Cleaning {dir.FullName()}");
            dir.CleanIfExists();
        }
    }
}
