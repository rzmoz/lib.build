using System;
using DotNet.Basics.IO;
using Serilog;

namespace Lib.Build
{
    public class SolutionBuilder
    {
        private readonly BuildArgs _args;

        public SolutionBuilder(BuildArgs args)
        {
            _args = args ?? throw new ArgumentNullException(nameof(args));
        }

        public void Init()
        {
            Log.Information("Initializing {SolutionBuilder}", nameof(SolutionBuilder));
            Log.Debug($"{nameof(_args.SolutionDir)}: {_args.SolutionDir?.FullName()}");
            Log.Debug($"{nameof(_args.Configuration)}: {_args.Configuration}");
            Log.Debug($"{nameof(_args.ArtifactsDir)}: {_args.ArtifactsDir?.FullName()}");
            Log.Debug($"{nameof(_args.Ps1CallbackRootDir)}: {_args.Ps1CallbackRootDir?.FullName()}");
        }
    }
}
