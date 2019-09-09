using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DotNet.Basics.Cli;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.PowerShell;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class BuildArgsBuilder
    {
        private static readonly string _releaseProjectFilterKey = "ReleaseProjectFilter";
        private static readonly string _testProjectFilterKey = "TestProjectFilter";
        private static readonly string _preBuildCallbackFilterKey = "PreBuildCallbackFilter";
        private static readonly string _postBuildCallbackFilterKey = "PostBuildCallbackFilter";

        private readonly ILogDispatcher _log;

        public BuildArgsBuilder(ILogDispatcher log)
        {
            _log = log ?? new VoidLogger();
        }

        public static IReadOnlyDictionary<string, string> KeyMappings { get; } = new Dictionary<string, string>
        {
            {"v", nameof(BuildArgs.Version)},
            {"c", nameof(BuildArgs.Configuration)},
            {"p", nameof(BuildArgs.Publish)},
            {"config", nameof(BuildArgs.Configuration)},
            {"sln", nameof(BuildArgs.SolutionDir)},
            {"slnDir", nameof(BuildArgs.SolutionDir)},
            {"release", _releaseProjectFilterKey},
            {"releaseFilter", _releaseProjectFilterKey},
            {"test", _testProjectFilterKey},
            {"testFilter", _testProjectFilterKey},
            {"preBuild", _preBuildCallbackFilterKey},
            {"preBuildFilter",_preBuildCallbackFilterKey},
            {"postBuild", _postBuildCallbackFilterKey},
            {"postBuildFilter", _postBuildCallbackFilterKey},
        };

        public BuildArgs Build(ICliConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var args = new BuildArgs
            {
                Configuration = ResolveConfiguration(config),
                SolutionDir = ResolveSolutionDir(config),
                Publish = config.Args.ToArray().IsSet(nameof(BuildArgs.Publish)),

            };
            args.ReleaseArtifactsDir = ResolveReleaseArtifactsDir(config, args.SolutionDir);
            args.TestArtifactsDir = ResolveTestArtifactsDir(config, args.SolutionDir);
            args.Version = ResolveVersion(config[nameof(BuildArgs.Version)], args.SolutionDir);
            args.TestProjects = ResolveFiles(args.SolutionDir, config[_testProjectFilterKey] ?? "*.tests.csproj", "Test Projects");
            args.ReleaseProjects = ResolveReleaseProjects(args.SolutionDir, config[_releaseProjectFilterKey] ?? "*.csproj", args.TestProjects);

            args.PreBuildCallbacks = ResolveFiles(args.SolutionDir, config[_preBuildCallbackFilterKey] ?? "*.PreBuild.Callback.ps1", nameof(args.PreBuildCallbacks));
            args.PostBuildCallbacks = ResolveFiles(args.SolutionDir, config[_postBuildCallbackFilterKey] ?? "*.PostBuild.Callback.ps1", nameof(args.PostBuildCallbacks));

            _log.Information($"{nameof(BuildArgs)} initialized with:");
            _log.Debug($"{nameof(BuildArgs.Configuration)}: {args.Configuration.Highlight()}");
            _log.Debug($"{nameof(BuildArgs.SolutionDir)}: {args.SolutionDir.FullName().Highlight()}");
            _log.Debug($"{nameof(BuildArgs.ReleaseArtifactsDir)}: {args.ReleaseArtifactsDir.FullName().Highlight()}");
            _log.Debug($"{nameof(BuildArgs.TestArtifactsDir)}: {args.TestArtifactsDir.FullName().Highlight()}");
            _log.Debug($"{nameof(BuildArgs.Publish)}: {args.Publish.ToString().Highlight()}");
            _log.Debug($"{nameof(BuildArgs.Version)}: {args.Version.SemVer20String.Highlight()}");

            _log.Debug($"{nameof(BuildArgs.ReleaseProjects)}: {JsonSerializer.Serialize(args.ReleaseProjects.Select(prj => prj.FullName()), new JsonSerializerOptions { WriteIndented = true })}");
            _log.Debug($"{nameof(BuildArgs.TestProjects)}: {JsonSerializer.Serialize(args.TestProjects.Select(prj => prj.FullName()), new JsonSerializerOptions { WriteIndented = true })}");

            _log.Debug($"{nameof(BuildArgs.PreBuildCallbacks)}: {JsonSerializer.Serialize(args.PreBuildCallbacks.Select(callback => callback.FullName()), new JsonSerializerOptions { WriteIndented = true })}");
            _log.Debug($"{nameof(BuildArgs.PostBuildCallbacks)}: {JsonSerializer.Serialize(args.PostBuildCallbacks.Select(callback => callback.FullName()), new JsonSerializerOptions { WriteIndented = true })}");
            return args;
        }

        private IReadOnlyList<FilePath> ResolveFiles(DirPath slnDir, string filter, string name)
        {
            _log.Debug($"Resolving {name} with filter : {filter}");
            try
            {
                return slnDir.EnumerateFiles(filter, SearchOption.AllDirectories).ToList(); ;
            }
            catch (IOException e)
            {
                _log.Warning($"{e.GetType().Name.RemoveSuffix("Exception")}: {e.Message} when resolving {name.Highlight()} with {filter.Highlight()}");
                return new List<FilePath>();
            }
        }
        private IReadOnlyList<FilePath> ResolveReleaseProjects(DirPath slnDir, string filter, IReadOnlyList<FilePath> testProjects)
        {
            var releaseProjects = ResolveFiles(slnDir, filter, "Release Projects").ToList();

            for (var i = 0; i < releaseProjects.Count; i++)
            {
                if (testProjects.Contains(testPrj => testPrj.FullName().Equals(releaseProjects[i].FullName(), StringComparison.InvariantCultureIgnoreCase)))
                    releaseProjects.RemoveAt(i);
            }

            return releaseProjects;
        }
        private SemVersion ResolveVersion(string version, DirPath slnDir)
        {
            var gitPath = slnDir.ToDir(".git").FullName();
            var shortHash = PowerShellCli.Run(_log, $"git --git-dir=\"{gitPath}\" log --pretty=format:'%h' -n 1").First().ToString();

            if (string.IsNullOrEmpty(version) == false)
            {
                var literalVersion = new SemVersion(version);
                if (string.IsNullOrEmpty(literalVersion.Metadata))
                    literalVersion.Metadata += shortHash;
                _log.Verbose($"Version resolved to {literalVersion.SemVer20String.Highlight()}");
                return literalVersion;
            }

            _log.Verbose($"Version not set. Resolving version from git tags in {gitPath}");

            var gitVersions = PowerShellCli.Run(_log, $"git --git-dir=\"{gitPath}\" tag -l *");

            _log.Verbose($"Git tags found:");
            gitVersions.ForEach(tag => _log.Verbose(tag.ToString()));

            if (gitVersions.Any() == false)
            {
                _log.Warning($"No tags found in git repo in {gitPath.Highlight()}");
                return new SemVersion(0, 0, 0);
            }

            var latestVersion = gitVersions.Select(v => new SemVersion(v)).Max();


            latestVersion.Metadata += shortHash;
            _log.Verbose($"Version resolved from git to {latestVersion.SemVer20String.Highlight()}");
            return latestVersion;
        }
        private DirPath ResolveTestArtifactsDir(ICliConfiguration config, DirPath slnDir)
        {
            return config[nameof(BuildArgs.TestArtifactsDir)]?.ToDir() ?? slnDir.Add(".testArtifacts");
        }
        private DirPath ResolveReleaseArtifactsDir(ICliConfiguration config, DirPath slnDir)
        {
            return config[nameof(BuildArgs.ReleaseArtifactsDir)]?.ToDir() ?? slnDir.Add(".releaseArtifacts");
        }
        private DirPath ResolveSolutionDir(ICliConfiguration config)
        {
            var slnDir = config[nameof(BuildArgs.SolutionDir), 0]?.ToDir();

            if (slnDir == null || slnDir.Name.StartsWith("-"))
            {
                slnDir = ".".ToDir().FullName().ToDir();
                _log.Information($"{nameof(BuildArgs.SolutionDir)} not set. Will try to resolve automatically.");
            }

            if (slnDir.Exists() == false)
                throw new BuildException($"Directory not found: {slnDir.FullName()}");
            do
            {
                _log.Debug($"Looking for sln files in {slnDir.FullName().Highlight()}");
                if (slnDir.EnumerateFiles("*.sln").Any())
                {
                    _log.Information($"SolutionDir resolved to: {slnDir.FullName().Highlight()}");
                    return slnDir;
                }

                slnDir = slnDir.Parent;
            } while (slnDir != null);

            throw new BuildException($"No solution files found in {".".ToDir().FullName()} or any parent dir");
        }
        private string ResolveConfiguration(ICliConfiguration config)
        {
            return config[nameof(BuildArgs.Configuration)] ?? "release";
        }
    }
}

