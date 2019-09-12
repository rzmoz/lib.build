using System.Threading.Tasks;
using DotNet.Basics.Cli;
using DotNet.Basics.Sys;

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
            var argsBuilder = new BuildArgsBuilder(host, host.Log);
            if (args.IsSet(ArgsFromDiskFlag))
                argsBuilder = argsBuilder.WithArgsFromDisk();
            var buildArgs = argsBuilder.Build();

            //run build
            return await host.RunAsync("Build",
                async (config, log) =>
                {
                    return await new BuildStepDispatcher().DispatchAsync(host, buildArgs).ConfigureAwait(false);
                },
                new CliHostOptions
                {
                    LongRunningOperationsPingInterval = 1.Minutes()
                }).ConfigureAwait(false);
        }
    }
}
