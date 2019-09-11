using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.Sys;
using DotNet.Basics.IO;
using DotNet.Basics.PowerShell;

namespace Lib.Build
{
    public class CallbackRunner
    {
        public async Task InvokeCallbacksAsync(IReadOnlyCollection<FilePath> callbacks, DirPath slnDir, DirPath releaseArtifactsDir, ILogDispatcher log)
        {
            if (log == null)
                log = new VoidLogger();

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
                            log.Debug,
                            error =>
                            {
                                log.Warning(error);
                                errors.AppendLine(error);
                            });

                        if (exitCode != 0)
                            throw new BuildException(errors.ToString());
                        return Task.CompletedTask;
                    }
                    catch (Exception e)
                    {
                        throw new BuildException(e.Message);
                    }
                }).ConfigureAwait(false);
            }
        }
    }
}
