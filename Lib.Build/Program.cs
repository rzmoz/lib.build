using System;
using System.Threading.Tasks;
using Serilog;

namespace Lib.Build
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                var artifactsBuilder = new ArtifactsBuilder(args);
                artifactsBuilder.Init();

                await artifactsBuilder.PreBuild.RunAsync().ConfigureAwait(false);
                await artifactsBuilder.Build.RunAsync().ConfigureAwait(false);
                await artifactsBuilder.PostBuild.RunAsync().ConfigureAwait(false);
                return 0;
            }
            catch (Exception e)
            {
                Log.Error(e, $"{e.Message}\r\n{e.InnerException?.Message}");
                return -1;
            }
        }
    }
}
