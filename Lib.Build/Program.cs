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
                await new SolutionPreBuild(buildArgs, log, callbackRunner).RunAsync().ConfigureAwait(false);
                new SolutionBuild(buildArgs, log).Run();
                await new SolutionPostBuild(buildArgs, log, callbackRunner).RunAsync().ConfigureAwait(false);


            }).ConfigureAwait(false);
        }
    }
}
