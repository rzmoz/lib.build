using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Basics.Cli;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    class Program
    {
        internal const string ArgsFromDiskFlag = "argsFromDisk";
        internal const string RunOnlyFlag = "runOnly";
        internal const string PreBuildFlag = "preBuild";
        internal const string PreBuildCallbackFlag = "preBuildCallback";
        internal const string BuildFlag = "build";
        internal const string PostBuildFlag = "postBuild";
        internal const string PostBuildCallbackFlag = "preBuildCallback";

        static async Task<int> Main(string[] args)
        {
#if DEBUG
            args.PauseIfDebug();
#endif
            var callbackRunner = new CallbackRunner();

            //init host
            var host = new CliHostBuilder(args, switchMappings => switchMappings.AddRange(BuildArgsBuilder.KeyMappings)).Build();
            //init build args
            var argsBuilder = new BuildArgsBuilder(host, host.Log);
            if (args.IsSet(ArgsFromDiskFlag))
                argsBuilder = argsBuilder.WithArgsFromDisk();
            var buildArgs = argsBuilder.Build();

            //init steps to run
            var stepsToRun = new List<string>();
            stepsToRun.AddRange(args.IsSet(RunOnlyFlag)
                ? host[RunOnlyFlag].Split('|', StringSplitOptions.RemoveEmptyEntries)
                : new[] { PreBuildFlag, PreBuildCallbackFlag, BuildFlag, PostBuildFlag, PostBuildCallbackFlag });

            host.Log.Debug($"Steps to run: {stepsToRun.JoinString()}");

            //run build
            return await host.RunAsync("Build", async (config, log) =>
            {
                if (ShouldRun(PreBuildFlag, stepsToRun))
                    new SolutionPreBuild(buildArgs, log).Run();

                if (ShouldRun(PreBuildCallbackFlag, stepsToRun))
                    await callbackRunner.InvokeCallbacksAsync(buildArgs.PreBuildCallbacks, buildArgs.SolutionDir, buildArgs.ReleaseArtifactsDir, log).ConfigureAwait(false);

                if (ShouldRun(BuildFlag, stepsToRun))
                    new SolutionBuild(buildArgs, log).Run();

                if (ShouldRun(PostBuildFlag, stepsToRun))
                    new SolutionPostBuild(buildArgs, log).Run();

                if (ShouldRun(PostBuildCallbackFlag, stepsToRun))
                    await callbackRunner.InvokeCallbacksAsync(buildArgs.PostBuildCallbacks, buildArgs.SolutionDir, buildArgs.ReleaseArtifactsDir, log).ConfigureAwait(false);

                return 0;
            }).ConfigureAwait(false);
        }

        private static bool ShouldRun(string flag, IList<string> stepsToRun)
        {
            if (stepsToRun == null) throw new ArgumentNullException(nameof(stepsToRun));
            return stepsToRun.Any(step => step.Equals(flag, StringComparison.OrdinalIgnoreCase));
        }
    }
}
