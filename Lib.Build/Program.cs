using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Basics.Cli;

namespace Lib.Build
{
    class Program
    {
        internal const string ArgsFromDiskFlag = "argsFromDisk";
        internal const string NoCallbacksFlag = "noCallbacks";
        internal const string RunOnlyFlag = "runOnly";
        internal const string PreBuildFlag = "preBuild";
        internal const string BuildFlag = "build";
        internal const string PostBuildFlag = "postBuild";



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

            //init discreteSteps
            var stepsToRun = new List<string>();
            stepsToRun.AddRange(args.IsSet(RunOnlyFlag)
                ? host[RunOnlyFlag].Split('|', StringSplitOptions.RemoveEmptyEntries)
                : new[] { PreBuildFlag, BuildFlag, PostBuildFlag });

            //run build
            return await host.RunAsync("Build", async (config, log) =>
            {
                if (stepsToRun.Any(step => step.Equals(PreBuildFlag, StringComparison.OrdinalIgnoreCase)))
                    new SolutionPreBuild(buildArgs, log).Run();

                if (args.IsSet(NoCallbacksFlag))
                    await callbackRunner.InvokeCallbacksAsync(buildArgs.PreBuildCallbacks, buildArgs.SolutionDir, buildArgs.ReleaseArtifactsDir, log).ConfigureAwait(false);

                if (stepsToRun.Any(step => step.Equals(BuildFlag, StringComparison.OrdinalIgnoreCase)))
                    new SolutionBuild(buildArgs, log).Run();

                if (stepsToRun.Any(step => step.Equals(PostBuildFlag, StringComparison.OrdinalIgnoreCase)))
                    new SolutionPostBuild(buildArgs, log).Run();

                if (args.IsSet(NoCallbacksFlag))
                    await callbackRunner.InvokeCallbacksAsync(buildArgs.PostBuildCallbacks, buildArgs.SolutionDir, buildArgs.ReleaseArtifactsDir, log).ConfigureAwait(false);

                return 0;
            }).ConfigureAwait(false);
        }
    }
}
