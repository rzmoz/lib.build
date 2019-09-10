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
    public class SolutionPreBuild
    {
        private readonly BuildArgs _args;
        private readonly CallbackRunner _callbackRunner;
        private readonly ILogDispatcher _slnLog;

        public SolutionPreBuild(BuildArgs args, ILogDispatcher slnLog, CallbackRunner callbackRunner)
        {
            _args = args;
            _callbackRunner = callbackRunner;
            _slnLog = slnLog.InContext(nameof(SolutionPreBuild));
        }

        public async Task RunAsync()
        {
            _slnLog.Information($"Starting {nameof(SolutionPreBuild)}");

            CleanDir(_args.ReleaseArtifactsDir);
            _args.ReleaseArtifactsDir.CreateIfNotExists();

            //add csproj bin dirs 
            var csprojBinDirs = _args.SolutionDir.EnumerateDirectories("*bin*", SearchOption.AllDirectories).OrderByDescending(dir => dir.FullName());
            csprojBinDirs.ForEachParallel(CleanDir);

            await _callbackRunner
                .InvokeCallbacksAsync(_args.PreBuildCallbacks, _args.SolutionDir, _args.ReleaseArtifactsDir, _slnLog)
                .ConfigureAwait(false);
        }

        private void CleanDir(DirPath dir)
        {
            _slnLog.Debug($"Cleaning {dir.FullName()}");
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
