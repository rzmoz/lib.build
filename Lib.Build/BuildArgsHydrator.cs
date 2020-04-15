using System;
using System.Collections.Generic;
using System.Linq;
using DotNet.Basics.Cli;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;
using LibGit2Sharp;

namespace Lib.Build
{
    public class BuildArgsHydrator : CliArgsHydrator<BuildArgs>
    {
        public static IReadOnlyDictionary<string, string> KeyMappings { get; } = new Dictionary<string, string>
        {
            {"c", nameof(BuildArgs.Configuration)},
            {"v", nameof(BuildArgs.Version)},
            {"slnDir", nameof(BuildArgs.SolutionDir)},
            {"s", nameof(BuildArgs.SolutionDir)},
            {"release", nameof(BuildArgs.ReleaseProjectFilter)},
            {"releaseFilter", nameof(BuildArgs.ReleaseProjectFilter)},
            {"exclude", nameof(BuildArgs.ExcludeProjectFilter)},
            {"excludeFilter", nameof(BuildArgs.ExcludeProjectFilter)},
            {"zip", nameof(BuildArgs.ZipIt)},
            {"p", nameof(BuildArgs.Publish)},
            {"r", nameof(BuildArgs.Runtime)},
        };

        public override BuildArgs Hydrate(ICliConfiguration config, BuildArgs args, ILogger log = null)
        {
            args.SolutionDir = ResolveSolutionDir(config, log);
            if (config.HasValue(nameof(BuildArgs.Version)) == false)
                args.Version = ResolveVersionFromGit(args.SolutionDir, log);
            if (config.HasValue(nameof(args.ReleaseProjects)) == false)
                args.ReleaseProjects = ResolveReleaseProjects(args.SolutionDir, args.ReleaseProjectFilter, args.ExcludeProjectFilter, log);

            if (args.ReleaseArtifactsDir.IsRooted() == false)
                args.ReleaseArtifactsDir = args.SolutionDir.Add(args.ReleaseArtifactsDir.RawPath);

            return args;
        }

        private IReadOnlyList<FilePath> ResolveReleaseProjects(DirPath slnDir, string releaseProjectFilter, string excludeProjectFilter, ILogger log)
        {
            var releaseProjects = ResolveFiles(slnDir, releaseProjectFilter.TrimEnd('.').EnsureSuffix(".csproj"), "Release Projects", log).ToList();
            var excludeProjects = ResolveFiles(slnDir, excludeProjectFilter.TrimEnd('.').EnsureSuffix(".csproj"), "Exclude Projects", log).ToList();
            for (var i = 0; i < releaseProjects.Count; i++)
            {
                if (excludeProjects.Contains(testPrj => testPrj.FullName().Equals(releaseProjects[i].FullName(), StringComparison.InvariantCultureIgnoreCase)))
                    releaseProjects.RemoveAt(i);
            }
            return releaseProjects;
        }

        private SemVersion ResolveVersionFromGit(DirPath solutionDir, ILogger log)
        {
            using var repo = new Repository(solutionDir.FullName());
            var shortHash = repo.Commits.First().Sha.Substring(0, 7);
            var tags = repo.Tags;
            var gitVersions = tags.Select(tag => SemVersion.Parse(tag.FriendlyName)).ToList();

            log.Verbose($"Git tags found:");
            gitVersions.ForEach(tag => log.Verbose(tag.ToString()));

            if (gitVersions.Any() == false)
            {
                log.Warning($"No tags found in git repo in {solutionDir}");
                return new SemVersion(0, 0, 0);
            }

            var latestVersion = gitVersions.Select(v => new SemVersion(v)).Max();

            latestVersion.Metadata += shortHash;
            log.Verbose($"Version resolved from git to {latestVersion.SemVer20String.Highlight()}");
            return latestVersion;
        }
    }
}