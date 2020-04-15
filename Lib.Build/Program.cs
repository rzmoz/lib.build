using System.Threading.Tasks;
using DotNet.Basics.Cli;
using DotNet.Basics.Diagnostics.Console;
using DotNet.Basics.IO;

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
            var host = new CliHostBuilder(args)
                .Build<BuildArgs>(switchMappings => switchMappings.AddRange(BuildArgsHydrator.KeyMappings), true, () => new AutoFromConfigHydrator<BuildArgs>(), () => new BuildArgsHydrator());

            //run build
            return await host.RunAsync($"Build {host.Args.SolutionDir.Name}", async (config, log) =>
            {
                var result = await new BuildStepDispatcher().DispatchAsync(host, host.Args).ConfigureAwait(false);
                //set output variables
                if (AzureDevOpsConsoleLogTarget.EnvironmentIsAzureDevOpsHostedAgent())
                    log.Raw($"##vso[task.setvariable variable={nameof(host.Args.ReleaseArtifactsDir)}]{host.Args.ReleaseArtifactsDir.FullName()}");

                return result;
            }).ConfigureAwait(false);
        }
    }
}
