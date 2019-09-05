using System.Linq;
using System.Threading.Tasks;
using DotNet.Basics.Cli;
using DotNet.Basics.Cli.ConsoleOutput;

namespace Lib.Build
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
#if DEBUG
            args.PauseIfDebug();
#endif
            var host = new CliHostBuilder(args, switchMappings => switchMappings.AddRange(BuildHost.KeyMappings))
                .WithLogging(config => config.AddConsole())
                .BuildCustomHost((args, config, log) => new BuildHost(args.ToArray(), config, log));

            return await host.RunAsync("Build", async (config, log) =>
             {
                 var artifactsBuilder = new ArtifactsBuilder(host, log);
                 artifactsBuilder.Init();

                 await artifactsBuilder.PreBuild.RunAsync().ConfigureAwait(false);
                 artifactsBuilder.Build.Run();
                 await artifactsBuilder.PostBuild.RunAsync().ConfigureAwait(false);

             }).ConfigureAwait(false);
        }
    }
}
