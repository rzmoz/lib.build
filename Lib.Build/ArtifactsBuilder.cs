using System;
using System.Diagnostics;
using System.Linq;
using DotNet.Basics.IO;
using Serilog;

namespace Lib.Build
{
    public class ArtifactsBuilder
    {
        private readonly BuildArgs _args;

        public ArtifactsBuilder(string[] args)
        {
            _args = new BuildArgs(args);
            PreBuild = new SolutionPreBuild(_args);
            Build = new SolutionBuild(_args);
            PostBuild = new SolutionPostBuild(_args);
        }

        public void Init()
        {
            if (_args.IsDebug)
            {
                Log.Warning("Paused for debug. PID: {ProcessId} | Name: {ProcessName}. Press {ENTER} to continue..", Process.GetCurrentProcess().Id, Process.GetCurrentProcess().ProcessName, "[ENTER]");
                Console.ReadLine();
            }

            Log.Information("Initializing {ArtifactsBuilder}", nameof(ArtifactsBuilder));
            Log.Debug($"{nameof(_args.SolutionDir)}: {_args.SolutionDir?.FullName()}");
            Log.Debug($"{nameof(_args.Configuration)}: {_args.Configuration}");
            Log.Debug($"{nameof(_args.ArtifactsDir)}: {_args.ArtifactsDir?.FullName()}");
            Log.Debug($"{nameof(_args.Ps1CallbackRootDir)}: {_args.Ps1CallbackRootDir?.FullName()}");

            ResolvePs1Callbacks();
            
        }

        private void ResolvePs1Callbacks()
        {
            Log.Information($"Resolving Ps1 callbacks");

            if (_args.Ps1CallbackRootDir?.Exists() == false)
                throw new BuildException($"{nameof(_args.Ps1CallbackRootDir)} not found at {_args.Ps1CallbackRootDir}");

            if (_args.Ps1CallbackRootDir == null)
            {
                Log.Debug($"{nameof(_args.Ps1CallbackRootDir)} not set. Skipping");
                return;
            }

            _args.PreBuildCallbacks = _args.Ps1CallbackRootDir.EnumerateFiles("*.PreBuild.Callback.ps1").OrderBy(file => file.Name).ToList();
            Log.Debug($"{nameof(_args.PreBuildCallbacks)} found: {_args.PreBuildCallbacks.Count}");

            _args.BuildCallbacks = _args.Ps1CallbackRootDir.EnumerateFiles("*.Build.Callback.ps1").OrderBy(file => file.Name).ToList();
            Log.Debug($"{nameof(_args.BuildCallbacks)} found: {_args.BuildCallbacks.Count}");

            _args.PostBuildCallbacks = _args.Ps1CallbackRootDir.EnumerateFiles("*.PostBuild.Callback.ps1").OrderBy(file => file.Name).ToList();
            Log.Debug($"{nameof(_args.PostBuildCallbacks)} found: {_args.PostBuildCallbacks.Count}");
        }

        public SolutionPreBuild PreBuild { get; }
        public SolutionBuild Build { get; }
        public SolutionPostBuild PostBuild { get; }
    }
}
