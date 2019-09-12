using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Basics.Cli;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class BuildStepDispatcher
    {
        private static readonly IReadOnlyList<BuildStep> _steps = new BuildStep[]
        {
            //order matters!
            new SolutionPreBuild(),
            new SolutionPreBuildCallBack(),
            new SolutionBuild(),
            new SolutionPostBuild(),
            new SolutionPostBuildCallBack(),
        };

        internal const string RunOnlyFlag = "runOnly";

        public async Task<int> DispatchAsync(CliHost host, BuildArgs args)
        {
            var stepsToRun = (host.Args.ToArray().IsSet(RunOnlyFlag)
                ? host[RunOnlyFlag].Split('|', StringSplitOptions.RemoveEmptyEntries)
                : _steps.Select(step => step.Flag)).ToList();
            
            host.Log.Info($"Enabled steps: {stepsToRun.JoinString()}");

            foreach (var buildStep in _steps)
                await TryDispatchAsync(buildStep, stepsToRun, host, args).ConfigureAwait(false);

            return 0;
        }

        public async Task<int> TryDispatchAsync(BuildStep step, IEnumerable<string> stepsToRun, CliHost host, BuildArgs args)
        {
            if (!ShouldRun(step.Flag, stepsToRun))
                return 0;

            host.Log.Info(GetBuildStart(step.Flag, args.SolutionDir.Name));
            var exitCode = await step.RunAsync(args, host.Log).ConfigureAwait(false);
            if (exitCode != 0)
                throw new BuildException($"{step.Flag} FAILED. See log for details", exitCode);

            return exitCode;
        }

        private static bool ShouldRun(string flag, IEnumerable<string> stepsToRun)
        {
            if (stepsToRun == null) throw new ArgumentNullException(nameof(stepsToRun));
            return stepsToRun.Any(step => step.Equals(flag, StringComparison.OrdinalIgnoreCase));
        }

        private string GetBuildStart(string step, string name) => $@"
*************************************************************************************

                        Starting {step.Highlight()} for {name}
                
*************************************************************************************
";
    }
}
