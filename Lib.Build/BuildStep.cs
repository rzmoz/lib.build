using System.IO;
using System.Threading.Tasks;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;
using DotNet.Basics.Tasks.Repeating;

namespace Lib.Build
{
    public abstract class BuildStep
    {
        protected BuildStep(string runFlag = null)
        {
            Flag = ResolveRunFlag(runFlag);
        }

        public string Flag { get; }

        public virtual Task<int> RunAsync(BuildArgs args, ILogger log)
        {
            return InnerRunAsync(args, log?.InContext(Flag));
        }
        protected abstract Task<int> InnerRunAsync(BuildArgs args, ILogger log);

        protected void InitDir(DirPath dir, ILogger log)
        {
            if (dir.Exists())
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
            else
            {
                dir.CreateIfNotExists();
            }
        }

        private string ResolveRunFlag(string runFlag)
        {
            return string.IsNullOrWhiteSpace(runFlag)
                ? GetType().Name.RemovePrefix("Solution")
                : runFlag;
        }
    }
}
