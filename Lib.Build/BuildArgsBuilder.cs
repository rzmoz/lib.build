using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotNet.Basics.Cli;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;
using LibGit2Sharp;

namespace Lib.Build
{
    public class BuildArgsBuilder
    {
        private static readonly string _releaseProjectFilterKey = "ReleaseProjectFilter";
        private static readonly string _testProjectFilterKey = "TestProjectFilter";
        private readonly ArgsFilesProvider _argsFilesProvider;

        private readonly ICliConfiguration _config;
        private readonly ILogDispatcher _log;

        private bool _loadArgsFromDisk;

        public static IReadOnlyDictionary<string, string> KeyMappings { get; } = new Dictionary<string, string>
        {
            {"v", nameof(BuildArgs.Version)},
            {"c", nameof(BuildArgs.Configuration)},
            {"config", nameof(BuildArgs.Configuration)},
            {"sln", nameof(BuildArgs.SolutionDir)},
            {"slnDir", nameof(BuildArgs.SolutionDir)},
            {"release", _releaseProjectFilterKey},
            {"test", _testProjectFilterKey},
            {"pack", nameof(BuildArgs.Package)}
        };

        public BuildArgsBuilder(ICliConfiguration config, ILogDispatcher log)
        {
            _config = config;
            _log = log ?? LogDispatcher.NullLogger;
            _argsFilesProvider = new ArgsFilesProvider(log);
            SolutionDir = ResolveSolutionDir(_config);
        }

        public DirPath SolutionDir { get; }

        public BuildArgsBuilder WithArgsFromDisk()
        {
            _loadArgsFromDisk = true;
            return this;
        }

        public BuildArgs Build()
        {
            BuildArgs args;
            if (_loadArgsFromDisk)
            {
                args = _argsFilesProvider.Load(SolutionDir.Name);
                if (args != null)
                    _log.Debug($"Args loaded from {_argsFilesProvider.SettingsPath(SolutionDir.Name)}");
                else
                    throw new BuildException($"ArgsFromDisk flag is set but args was NOT loaded from {_argsFilesProvider.SettingsPath(SolutionDir.Name)}", 500);
            }
            else
                args = ResolveArgsFromContext();

            _argsFilesProvider.Save(SolutionDir.Name, args);

            return args;
        }

        private BuildArgs ResolveArgsFromContext()
        {
            _log.Debug($"Resolving args from context");

            //general properties
            var args = new BuildArgs
            {
                Configuration = _config[nameof(BuildArgs.Configuration)] ?? "release",
                SolutionDir = SolutionDir,
                Version = _config.HasValue(nameof(BuildArgs.Version)) ? _config[nameof(BuildArgs.Version)].ToSemVersion() : ResolveVersionFromGit(),
            };

            //release settings
            args.ReleaseArtifactsDir = _config[nameof(BuildArgs.ReleaseArtifactsDir)]?.ToDir() ?? args.SolutionDir.Add(".releaseArtifacts");
            args.Package = _config.IsSet(nameof(BuildArgs.Package));

            //test settings
            args.TestFilter = _config[nameof(BuildArgs.TestFilter)];
            args.WithTestResults = _config.Args.IsSet(nameof(BuildArgs.WithTestResults));
            args.TestResultsDir = _config[nameof(BuildArgs.TestResultsDir)]?.ToDir() ?? SolutionDir.Add(".testResults");

            //resolve projects
            args.TestProjects = ResolveFiles(args.SolutionDir, (_config[_testProjectFilterKey] ?? "*.tests").EnsureSuffix(".csproj"), "Test Projects");
            //MUST be resolved AFTER test projects are resolved
            args.ReleaseProjects = ResolveReleaseProjects(args.SolutionDir, (_config[_releaseProjectFilterKey] ?? "*").EnsureSuffix(".csproj"), args.TestProjects);

            return args;
        }

        private IReadOnlyList<FilePath> ResolveFiles(DirPath slnDir, string filter, string name)
        {
            _log.Debug($"Resolving {name} with filter: {filter}");
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

        private SemVersion ResolveVersionFromGit()
        {
            using var repo = new Repository(SolutionDir.FullName());
            var shortHash = repo.Commits.First().Sha.Substring(0, 7);
            var tags = repo.Tags;
            var gitVersions = tags.Select(tag => SemVersion.Parse(tag.FriendlyName)).ToList();

            _log.Verbose($"Git tags found:");
            gitVersions.ForEach(tag => _log.Verbose(tag.ToString()));

            if (gitVersions.Any() == false)
            {
                _log.Warning($"No tags found in git repo in {SolutionDir}");
                return new SemVersion(0, 0, 0);
            }

            var latestVersion = gitVersions.Select(v => new SemVersion(v)).Max();

            latestVersion.Metadata += shortHash;
            _log.Verbose($"Version resolved from git to {latestVersion.SemVer20String.Highlight()}");
            return latestVersion;
        }

        private DirPath ResolveSolutionDir(ICliConfiguration config)
        {
            var slnDir = config[nameof(BuildArgs.SolutionDir), 0]?.ToDir();

            if (slnDir == null || slnDir.Name.StartsWith("-"))
            {
                slnDir = ".".ToDir().FullName().ToDir();
                _log.Info($"{nameof(BuildArgs.SolutionDir)} not set. Will try to resolve automatically.");
            }

            if (slnDir.Exists() == false)
                throw new BuildException($"Directory not found: {slnDir.FullName()}", 400);
            do
            {
                _log.Debug($"Looking for sln files in {slnDir.FullName().Highlight()}");
                if (slnDir.EnumerateFiles("*.sln").Any())
                {
                    _log.Info($"SolutionDir resolved to: {slnDir.FullName().Highlight()}");
                    return slnDir;
                }

                slnDir = slnDir.Parent;
            } while (slnDir != null);

            throw new BuildException($"No solution files found in {".".ToDir().FullName()} or any parent dir", 400);
        }
    }
}