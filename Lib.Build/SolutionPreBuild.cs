using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.PowerShell;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class SolutionPreBuild
    {
        private readonly BuildHost _host;
        private readonly ILogDispatcher _slnLog;

        public SolutionPreBuild(BuildHost host, ILogDispatcher slnLog)
        {
            _host = host;
            _slnLog = slnLog.InContext(nameof(SolutionPreBuild));
        }

        public async Task RunAsync()
        {
            _slnLog.Information($"Starting {nameof(SolutionPreBuild)}");

            CleanDir(_host.ArtifactsDir);
            _host.ArtifactsDir.CreateIfNotExists();

            //add csproj bin dirs 
            var csprojBinDirs = _host.SolutionDir.EnumerateDirectories("*bin*", SearchOption.AllDirectories);
            csprojBinDirs.ForEachParallel(CleanDir);

            if (_host.PreBuildCallbacks.Any())
            {
                _slnLog.Information($"Solution PreBuild callbacks found.");
                foreach (var solutionPreBuildCallback in _host.PreBuildCallbacks)
                {
                    _slnLog.Verbose($"Invoking {solutionPreBuildCallback.FullName()}");
                    await LongRunningOperations.StartAsync(solutionPreBuildCallback.Name, () =>
                            {
                                PowerShellCli.Run(_slnLog, new PowerShellCmdlet($"& \"{solutionPreBuildCallback.FullName()}\"")
                                      .WithParam("slnDir", _host.SolutionDir.FullName())
                                      .WithParam("artifactsDir", _host.ArtifactsDir.FullName())
                                      .WithVerbose()
                                );
                                return Task.CompletedTask;
                            }).ConfigureAwait(false);
                }
            }
        }

        private void CleanDir(DirPath dir)
        {
            _slnLog.Debug($"Cleaning {dir.FullName()}");
            try
            {
                dir.CleanIfExists();
            }
            catch (DirectoryNotFoundException)
            {
                //ignore
            }
        }
    }
}
