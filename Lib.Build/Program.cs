using System.Linq;
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
            var host = new CliHostBuilder(args, switchMappings => switchMappings.AddRange(BuildHost.KeyMappings))
                .BuildCustomHost((args, config, log) => new BuildHost(args.ToArray(), config, log));

            return await host.RunAsync("Build", (config, log) =>
            {
                var artifactsBuilder = new ArtifactsBuilder(host, log);
                artifactsBuilder.Init();
                artifactsBuilder.PreBuild.Run();
                artifactsBuilder.Build.Run();
                artifactsBuilder.PostBuild.Run();
                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }
    }
}
