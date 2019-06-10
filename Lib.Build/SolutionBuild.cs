﻿using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.PowerShell;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class SolutionBuild
    {
        private readonly BuildArgs _args;
        private readonly ILogDispatcher _slnLog;

        private const string _tempFileSuffix = ".temp";

        public SolutionBuild(BuildArgs args, ILogDispatcher slnLog)
        {
            _args = args;
            _slnLog = slnLog.InContext(nameof(SolutionBuild));
        }

        public void Run()
        {
            _slnLog.Information($"Starting {nameof(SolutionBuild)}");

            try
            {
                var version = _args.Version ?? GetVersionFromGit(_args.SolutionDir);
                _args.ReleaseProjects.ForEachParallel(csproj => UpdateVersion(csproj, version));

                _slnLog.Debug($"Looking for Solution files in {_args.SolutionDir}");
                var solutionFiles = _args.SolutionDir.EnumerateFiles("*.sln").ToList();
                _slnLog.Debug($"Found: {solutionFiles.Select(sln => sln.Name).JoinString().Highlight()}");

                foreach (var solutionFile in solutionFiles)
                {
                    var publishAction = $"publish \"{solutionFile.FullName()}\" --configuration {_args.Configuration} --force --verbosity quiet";
                    var buildAction = $" build \"{solutionFile.FullName()}\" --configuration {_args.Configuration} --no-incremental --verbosity quiet";
                    var action = _args.Publish ? publishAction : buildAction;

                    _slnLog.Information(action.Highlight());

                    var result = ExternalProcess.Run("dotnet", action, _slnLog.Debug, _slnLog.Error);
                    if (result.ExitCode != 0)
                        throw new BuildException($"Build failed for {solutionFile.FullName()}. See logs for details");
                }
            }
            finally
            {
                _args.ReleaseProjects.ForEachParallel(RevertVersion);
            }
        }

        private SemVersion GetVersionFromGit(DirPath solutionDir)
        {
            var gitPath = solutionDir.ToDir(".git").FullName();
            _slnLog.Debug($"Trying to resolve version from git in {gitPath}");

            var gitVersions = PowerShellCli.RunScript($"git --git-dir=\"{gitPath}\" tag -l v*");

            if (gitVersions.Any() == false)
            {
                _slnLog.Warning($"No tags found in git repo in {gitPath.Highlight()}");
                return new SemVersion(0, 0, 0);
            }

            var latestVersion = gitVersions.Select(v => new SemVersion(v)).Max();
            var shortHash = PowerShellCli.RunScript($"git --git-dir=\"{gitPath }\" log --pretty=format:'%h' -n 1").First().ToString();

            latestVersion.Metadata += shortHash;
            _slnLog.Debug($"Version resolved from git to {latestVersion.SemVer20String.Highlight()}");
            return latestVersion;
        }

        private void UpdateVersion(FilePath projectFile, SemVersion version)
        {
            var tmpFile = GetTempFilePath(projectFile);
            _slnLog.Debug($"Backing up {projectFile.FullName()} to {tmpFile.FullName()}");
            projectFile.CopyTo(tmpFile, overwrite: true);

            var projectXml = projectFile.ReadAllText();
            var projectXDoc = XDocument.Parse(projectXml);
            var propertyGroupElement = projectXDoc.Root.XPathSelectElement("//Project/PropertyGroup");
            if (propertyGroupElement == null)
                throw new BuildException($"Failed to update version for {projectFile.FullName()}. Csproj file format seems to be wrong");

            EnsureNodeWithValue(propertyGroupElement, "Version", version.SemVer20String);
            EnsureNodeWithValue(propertyGroupElement, "AssemblyVersion", version.SemVer10String);
            EnsureNodeWithValue(propertyGroupElement, "FileVersion", version.SemVer10String);
            _slnLog.Debug($"Updating {projectFile.Name.Highlight()} to {version.SemVer20String.Highlight()}");
            using (var writer = new StreamWriter(projectFile.FullName()))
            using (var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings { Indent = true }))
            {
                projectXDoc.Save(xmlWriter);
            }
        }

        private static void EnsureNodeWithValue(XElement parentNode, string nodeName, string value)
        {
            var node = parentNode.XPathSelectElement($"//{nodeName}");
            if (node == null)
                parentNode.Add(new XElement(nodeName, value));
            else
                node.Value = value;
        }

        private void RevertVersion(FilePath projectFile)
        {
            var tmpFile = GetTempFilePath(projectFile);
            if (tmpFile.Exists() == false)
            {
                _slnLog.Debug($"{tmpFile.FullName()} not found.");
            }
            else
            {
                _slnLog.Debug($"Reverting {tmpFile.FullName()} to {projectFile.FullName()}");
                tmpFile.MoveTo(projectFile, overwrite: true, ensureTargetDir: false);
            }
        }

        private static FilePath GetTempFilePath(FilePath projectFile)
        {
            return (projectFile.FullName() + _tempFileSuffix).ToFile();
        }
    }
}
