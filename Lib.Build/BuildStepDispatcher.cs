using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Basics.Cli;

namespace Lib.Build
{
    public class BuildStepDispatcher
    {
        private readonly BuildStep _preBuild;
        private readonly BuildStep _preBuildCallBack;
        private readonly BuildStep _build;
        private readonly BuildStep _postBuild;
        private readonly BuildStep _postBuildCallBack;

        internal const string RunOnlyFlag = "runOnly";

        public BuildStepDispatcher()
        {
            _preBuild = new SolutionPreBuild();
            _preBuildCallBack = new SolutionPreBuildCallBack();
            _build = new SolutionBuild();
            _postBuild = new SolutionPostBuild();
            _postBuildCallBack = new SolutionPostBuildCallBack();
        }

        public async Task<int> DispatchAsync(CliHost host, BuildArgs args)
        {
            var stepsToRun = new List<string>();
            stepsToRun.AddRange(host.Args.ToArray().IsSet(RunOnlyFlag)
                ? host[RunOnlyFlag].Split('|', StringSplitOptions.RemoveEmptyEntries)
                : new[] { _preBuild.Flag, _preBuildCallBack.Flag, _build.Flag, _postBuild.Flag, _postBuildCallBack.Flag });

            await TryDispatchAsync(_preBuild, stepsToRun, host, args).ConfigureAwait(false);
            await TryDispatchAsync(_preBuildCallBack, stepsToRun, host, args).ConfigureAwait(false);
            await TryDispatchAsync(_build, stepsToRun, host, args).ConfigureAwait(false);
            await TryDispatchAsync(_postBuild, stepsToRun, host, args).ConfigureAwait(false);
            await TryDispatchAsync(_postBuildCallBack, stepsToRun, host, args).ConfigureAwait(false);
            return 0;
        }

        public async Task<int> TryDispatchAsync(BuildStep step, IList<string> stepsToRun, CliHost host, BuildArgs args)
        {
            if (!ShouldRun(step.Flag, stepsToRun))
                return 0;

            var exitCode = await step.RunAsync(args, host.Log).ConfigureAwait(false);
            if (exitCode != 0)
                throw new BuildException($"{step.Flag} FAILED. See log for details", exitCode);

            return exitCode;
        }

        private static bool ShouldRun(string flag, IList<string> stepsToRun)
        {
            if (stepsToRun == null) throw new ArgumentNullException(nameof(stepsToRun));
            return stepsToRun.Any(step => step.Equals(flag, StringComparison.OrdinalIgnoreCase));
        }
    }
}
