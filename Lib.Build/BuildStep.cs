using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.PowerShell;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public abstract class BuildStep
    {
        protected BuildStep(string runFlag = null)
        {
            Flag = ResolveRunFlag(runFlag);
        }

        public string Flag { get; }

        public virtual Task<int> RunAsync(BuildArgs args, ILogDispatcher log)
        {
            return InnerRunAsync(args, log?.InContext(Flag));
        }
        protected abstract Task<int> InnerRunAsync(BuildArgs args, ILogDispatcher log);

        protected async Task<int> InvokeCallbacksAsync(IReadOnlyCollection<FilePath> callbacks, DirPath slnDir, DirPath releaseArtifactsDir, ILogDispatcher log)
        {
            if (log == null)
                log = LogDispatcher.NullLogger;

            foreach (var callback in callbacks)
            {
                log.Debug($"Invoking {callback.FullName()}");
                log.Verbose($"{callback.FullName()}\r\n{callback.ReadAllText()}");
                await LongRunningOperations.StartAsync(callback.Name, () =>
                {
                    try
                    {
                        var errors = new StringBuilder();
                        var exitCode = PowerShellCli.RunFileInConsole(
                            $"{callback.FullName()} -slnDir {slnDir.FullName()} -artifactsDir {releaseArtifactsDir.FullName()}",
                            output => log.Debug(output),
                            error =>
                            {
                                log.Warning(error);
                                errors.AppendLine(error);
                            });

                        if (exitCode != 0)
                            throw new BuildException(errors.ToString(), exitCode);
                        return Task.CompletedTask;
                    }
                    catch (Exception e)
                    {
                        throw new BuildException(e.Message, 500);
                    }
                }).ConfigureAwait(false);
            }

            return 0;
        }

        private string ResolveRunFlag(string runFlag)
        {
            return string.IsNullOrWhiteSpace(runFlag)
                ? GetType().Name.RemovePrefix("Solution")
                : runFlag;
        }
    }
}
