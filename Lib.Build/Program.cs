using System;
using DotNet.Basics.Cli;
using Serilog;

namespace Lib.Build
{
    class Program
    {
        static int Main(string[] args)
        {
#if DEBUG
            args.PauseIfDebug();
#endif
            try
            {
                var startTimestamp = DateTime.UtcNow;
                var artifactsBuilder = new ArtifactsBuilder(args);
                artifactsBuilder.Init();

                artifactsBuilder.PreBuild.Run();
                artifactsBuilder.Build.Run();
                artifactsBuilder.PostBuild.Run();

                var endTimestamp = DateTime.UtcNow;
                var duration = endTimestamp - startTimestamp;
                Log.Information($"Build completed in {duration:g}");
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
