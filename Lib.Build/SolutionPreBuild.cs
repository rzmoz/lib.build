using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;

namespace Lib.Build
{
    public class SolutionPreBuild : BuildStep
    {
        protected override Task<int> InnerRunAsync(BuildArgs args, ILogDispatcher log)
        {
            log.Info($"Starting {nameof(SolutionPreBuild)}");

            InitDir(args.ReleaseArtifactsDir, log);

            //add csproj bin dirs 
            var csprojBinDirs = args.SolutionDir.EnumerateDirectories("bin", SearchOption.AllDirectories).OrderByDescending(dir => dir.FullName());
            csprojBinDirs.ForEachParallel(binDir => InitDir(binDir, log));
            return Task.FromResult(0);
        }
    }
}
