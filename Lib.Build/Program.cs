using System.Threading.Tasks;
using DotNet.Basics.Cli;

namespace Lib.Build
{
    class Program
    {
        internal const string ArgsFromDiskFlag = "argsFromDisk";

        static async Task<int> Main(string[] args)
        {
#if DEBUG
            args.PauseIfDebug();
#endif
            //init host
            var host = new CliHostBuilder(args, switchMappings => switchMappings.AddRange(BuildArgsBuilder.KeyMappings)).Build();

            //init build args
            var argsBuilder = new BuildArgsBuilder(host, host.Log.InContext("Init"));
            if (args.IsSet(ArgsFromDiskFlag))
                argsBuilder = argsBuilder.WithArgsFromDisk();
            var buildArgs = argsBuilder.Build();

            //run build
            return await host.RunAsync($"Build {buildArgs.SolutionDir.Name}", async (config, log) => await new BuildStepDispatcher().DispatchAsync(host, buildArgs).ConfigureAwait(false)).ConfigureAwait(false);
        }
    }
}
