using System;
using System.IO;
using System.Linq;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class ArtifactsBuilder
    {
        private readonly ILogDispatcher _initLog;
        private readonly BuildHost _host;

        public ArtifactsBuilder(BuildHost host, ILogDispatcher log)
        {
            _initLog = log.InContext("Init");
            _host = host ?? throw new ArgumentNullException(nameof(host));
            PreBuild = new SolutionPreBuild(_host, log);
            Build = new SolutionBuild(_host, log);
            PostBuild = new SolutionPostBuild(_host, log);
        }

        public void Init()
        {
            _host.SolutionDir = VerifySolutionDir(_host.SolutionDir);

            _initLog.Information($"Initializing {nameof(ArtifactsBuilder)}");
            _initLog.Debug($"{nameof(_host.SolutionDir).Highlight()}: {_host.SolutionDir?.FullName()}");
            _initLog.Debug($"{nameof(_host.Configuration).Highlight()}: {_host.Configuration}");
            _initLog.Debug($"{nameof(_host.Version).Highlight()}: {_host.Version}");
            _initLog.Debug($"{nameof(_host.ArtifactsDir).Highlight()}: {_host.ArtifactsDir?.FullName()}");
            _initLog.Debug($"{nameof(_host.ReleaseProjectFilter).Highlight()}: {_host.ReleaseProjectFilter}");
            _initLog.Debug($"{nameof(_host.TestProjectFilter).Highlight()}: {_host.TestProjectFilter}");
            _initLog.Debug($"{nameof(_host.PreBuildCallbackFilter).Highlight()}: {_host.PreBuildCallbackFilter}");
            _initLog.Debug($"{nameof(_host.PostBuildCallbackFilter).Highlight()}: {_host.PostBuildCallbackFilter}");
            _initLog.Debug($"{nameof(_host.Publish).Highlight()}: {_host.Publish}");

            if (_host.SolutionDir.Exists() == false)
                throw new DirectoryNotFoundException($"Solution Dir not found: {_host.SolutionDir.FullName()}");

            ResolveProjects();
            ResolveCallbacks();
        }

        private DirPath VerifySolutionDir(DirPath currentDir)
        {
            if (currentDir == null || currentDir.Name.StartsWith("-"))
            {
                currentDir = ".".ToDir().FullName().ToDir();
                _initLog.Debug($"SolutionDir not set. Will try to resolve from first dir with sln file");
            }

            if (currentDir.Exists() == false)
                throw new DirectoryNotFoundException(currentDir.FullName());

            do
            {
                _initLog.Debug($"Looking for sln files in {currentDir.FullName()}");
                if (currentDir.EnumerateFiles("*.sln").Any())
                {
                    _initLog.Debug($"SolutionDir resolved to: {currentDir.FullName()}. ");
                    return currentDir;
                }

                currentDir = currentDir.Parent;
            } while (currentDir != null);

            throw new BuildException($"No solution files found in {".".ToDir().FullName()} or any parent dir");
        }

        private void ResolveCallbacks()
        {
            _initLog.Information($"Resolving Solution Callbacks");
            _host.PreBuildCallbacks = _host.SolutionDir.EnumerateFiles(_host.PreBuildCallbackFilter, SearchOption.AllDirectories).ToList();
            _host.PostBuildCallbacks = _host.SolutionDir.EnumerateFiles(_host.PostBuildCallbackFilter, SearchOption.AllDirectories).ToList();

            foreach (var callback in _host.PreBuildCallbacks)
                _initLog.Debug($"Solution PreBuild callback found: {callback.FullName()}");

            foreach (var callback in _host.PostBuildCallbacks)
                _initLog.Debug($"Solution PostBuild callback found: {callback.FullName()}");
        }


        private void ResolveProjects()
        {
            _initLog.Information($"Resolving Projects");
            var releaseProjects = _host.SolutionDir.EnumerateFiles(_host.ReleaseProjectFilter, SearchOption.AllDirectories).ToList();
            _host.TestProjects = _host.SolutionDir.EnumerateFiles(_host.TestProjectFilter, SearchOption.AllDirectories).ToList();

            foreach (var testProject in _host.TestProjects)
            {
                _initLog.Debug($"Test project found: {testProject.FullName()}");
                for (var i = 0; i < releaseProjects.Count; i++)
                {
                    if (releaseProjects[i].FullName().Equals(testProject.FullName()))
                    {
                        releaseProjects.RemoveAt(i);
                        break;
                    }
                }
            }
            _host.ReleaseProjects = releaseProjects;

            foreach (var releaseProject in _host.ReleaseProjects)
                _initLog.Debug($"Release project found: {releaseProject.FullName()}");

            if (_host.ReleaseProjects.Count == 0)
                throw new BuildException($"No release projects found under {_host.SolutionDir.FullName()} with release filter: {_host.ReleaseProjectFilter}");
        }

        public SolutionPreBuild PreBuild { get; }
        public SolutionBuild Build { get; }
        public SolutionPostBuild PostBuild { get; }
    }
}
