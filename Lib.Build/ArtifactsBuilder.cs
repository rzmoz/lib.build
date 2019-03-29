using System;
using System.Diagnostics;
using System.IO;
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
            if (_args.SolutionDir == null)
                _args.SolutionDir = ".".ToDir();

            Log.Information("Initializing {ArtifactsBuilder}", nameof(ArtifactsBuilder));
            Log.Debug($"{nameof(_args.SolutionDir)}: {_args.SolutionDir?.FullName()}");
            Log.Debug($"{nameof(_args.Configuration)}: {_args.Configuration}");
            Log.Debug($"{nameof(_args.ArtifactsDir)}: {_args.ArtifactsDir?.FullName()}");
            Log.Debug($"{nameof(_args.Ps1CallbackRootDir)}: {_args.Ps1CallbackRootDir?.FullName()}");

            if (_args.SolutionDir.Exists() == false)
                throw new DirectoryNotFoundException($"Solution Dir not found: {_args.SolutionDir.FullName()}");

            ResolveProjects();
            ResolvePs1Callbacks();
        }

        private void ResolveProjects()
        {
            Log.Information($"Resolving Projects");
            var releaseProjects = _args.SolutionDir.EnumerateFiles(_args.ReleaseProjectFilter, SearchOption.AllDirectories).ToList();
            _args.TestProjects = _args.SolutionDir.EnumerateFiles(_args.TestProjectFilter, SearchOption.AllDirectories).ToList();


            foreach (var testProject in _args.TestProjects)
            {
                Log.Debug($"Test project found: {testProject.FullName()}");
                for (var i = 0; i < releaseProjects.Count; i++)
                {
                    if (releaseProjects[i].FullName().Equals(testProject.FullName()))
                    {
                        releaseProjects.RemoveAt(i);
                        break;
                    }
                }
            }
            _args.ReleaseProjects = releaseProjects;

            foreach (var releaseProject in _args.ReleaseProjects)
                Log.Debug($"Release project found: {releaseProject.FullName()}");

            if (_args.ReleaseProjects.Count == 0)
                throw new BuildException($"No release projects found under {_args.SolutionDir.FullName()} with release filter: {_args.ReleaseProjectFilter}");
        }
        private void ResolvePs1Callbacks()
        {
            Log.Information($"Resolving Ps1 callbacks");

            if (_args.Ps1CallbackRootDir?.Exists() == false)
                throw new BuildException($"{nameof(_args.Ps1CallbackRootDir)} not found at {_args.Ps1CallbackRootDir}");

            if (_args.Ps1CallbackRootDir == null)
            {
                Log.Information($"{nameof(_args.Ps1CallbackRootDir)} not set. Skipping");
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
