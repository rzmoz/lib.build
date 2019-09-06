using System.Collections.Generic;
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
            foreach (var callback in callbacks)
            {
                log?.Debug($"Invoking {callback.FullName()}");
                log?.Verbose($"{callback.FullName()}\r\n{callback.ReadAllText()}");
                await LongRunningOperations.StartAsync(callback.Name, () =>
                {
                    PowerShellCli.Run(log, new PowerShellCmdlet($"& \"{callback.FullName()}\"")
                        .WithParam("slnDir", slnDir.FullName())
                        .WithParam("artifactsDir", releaseArtifactsDir.FullName())
                        .WithVerbose()
                    );
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            }
        }

    }
}
