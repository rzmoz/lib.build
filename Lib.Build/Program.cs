using System.Threading.Tasks;
using DotNet.Basics.Cli;

namespace Lib.Build
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
#if DEBUG
            args.PauseIfDebug();
#endif
            //init host
            var host = new CliHostBuilder(args, switchMappings => switchMappings.AddRange(BuildArgsBuilder.KeyMappings)).Build();

            //init build args
            var buildArgs = new BuildArgsBuilder(host.Log).Build(host);

            //run build
            return await host.RunAsync("Build", async (config, log) =>
            {
                var callbackRunner = new CallbackRunner();

                new SolutionPreBuild(buildArgs, log).Run();

                if (args.IsSet("nocallbacks", false) == false)
                    await callbackRunner.InvokeCallbacksAsync(buildArgs.PreBuildCallbacks, buildArgs.SolutionDir, buildArgs.ReleaseArtifactsDir, log).ConfigureAwait(false);

                if (args.IsSet("nobuild", false) == false)
                    new SolutionBuild(buildArgs, log).Run();

                new SolutionPostBuild(buildArgs, log).Run();

                if (args.IsSet("nocallbacks", false) == false)
                    await callbackRunner.InvokeCallbacksAsync(buildArgs.PostBuildCallbacks, buildArgs.SolutionDir, buildArgs.ReleaseArtifactsDir, log).ConfigureAwait(false);
                return 0;
            }).ConfigureAwait(false);
        }
    }
}
