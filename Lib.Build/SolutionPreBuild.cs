using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;
using DotNet.Basics.Tasks.Repeating;

namespace Lib.Build
{
    public class SolutionPreBuild : BuildStep
    {
        protected override Task<int> InnerRunAsync(BuildArgs args, ILogDispatcher log)
        {
            log.Information($"Starting {nameof(SolutionPreBuild)}");

            CleanDir(args.ReleaseArtifactsDir, log);
            args.ReleaseArtifactsDir.CreateIfNotExists();

            //add csproj bin dirs 
            var csprojBinDirs = args.SolutionDir.EnumerateDirectories("bin", SearchOption.AllDirectories).OrderByDescending(dir => dir.FullName());
            csprojBinDirs.ForEachParallel(binDir => CleanDir(binDir, log));
            return Task.FromResult(0);
        }

        private void CleanDir(DirPath dir, ILogDispatcher log)
        {
            log.Debug($"Cleaning {dir.FullName()}");
            try
            {
                Repeat.Task(() => dir.CleanIfExists())
                    .WithOptions(o =>
                    {
                        o.MaxTries = 3;
                        o.RetryDelay = 3.Seconds();
                    }).UntilNoExceptions();
            }
            catch (DirectoryNotFoundException)
            {
                //ignore
            }
        }
    }
}
