using System.IO;
using System.Linq;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;

namespace Lib.Build
{
    public class ArtifactsBuilder
    {
        private readonly ILogDispatcher _initLog;
        private readonly BuildArgs _args;

        public ArtifactsBuilder(string[] args, ILogDispatcher log)
        {
            _initLog = log.InContext("Init");
            _args = new BuildArgs(args);
            PreBuild = new SolutionPreBuild(_args, log);
            Build = new SolutionBuild(_args, log);
            PostBuild = new SolutionPostBuild(_args, log);
        }

        public void Init()
        {
            if (_args.SolutionDir == null)
                _args.SolutionDir = ".".ToDir();

            _initLog.Information($"Initializing {nameof(ArtifactsBuilder)}");
            _initLog.Debug($"{nameof(_args.SolutionDir).Highlight()}: {_args.SolutionDir?.FullName()}");
            _initLog.Debug($"{nameof(_args.Configuration).Highlight()}: {_args.Configuration}");
            _initLog.Debug($"{nameof(_args.Version).Highlight()}: {_args.Version}");
            _initLog.Debug($"{nameof(_args.ArtifactsDir).Highlight()}: {_args.ArtifactsDir?.FullName()}");
            _initLog.Debug($"{nameof(_args.ReleaseProjectFilter).Highlight()}: {_args.ReleaseProjectFilter}");
            _initLog.Debug($"{nameof(_args.TestProjectFilter).Highlight()}: {_args.TestProjectFilter}");
            _initLog.Debug($"{nameof(_args.Publish).Highlight()}: {_args.Publish}");

            if (_args.SolutionDir.Exists() == false)
                throw new DirectoryNotFoundException($"Solution Dir not found: {_args.SolutionDir.FullName()}");

            ResolveProjects();
        }

        private void ResolveProjects()
        {
            _initLog.Information($"Resolving Projects");
            var releaseProjects = _args.SolutionDir.EnumerateFiles(_args.ReleaseProjectFilter, SearchOption.AllDirectories).ToList();
            _args.TestProjects = _args.SolutionDir.EnumerateFiles(_args.TestProjectFilter, SearchOption.AllDirectories).ToList();

            foreach (var testProject in _args.TestProjects)
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
            _args.ReleaseProjects = releaseProjects;

            foreach (var releaseProject in _args.ReleaseProjects)
                _initLog.Debug($"Release project found: {releaseProject.FullName()}");

            if (_args.ReleaseProjects.Count == 0)
                throw new BuildException($"No release projects found under {_args.SolutionDir.FullName()} with release filter: {_args.ReleaseProjectFilter}");
        }

        public SolutionPreBuild PreBuild { get; }
        public SolutionBuild Build { get; }
        public SolutionPostBuild PostBuild { get; }
    }
}
