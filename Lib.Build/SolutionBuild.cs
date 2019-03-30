using System.IO;
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

        private const string _tempFileSuffix = ".temp";

        public SolutionBuild(BuildArgs args)
        {
            _args = args;
        }

        public void Run()
        {
            Log.Information($"Starting {nameof(SolutionBuild)}");

            try
            {
                var version = _args.Version ?? GetVersionFromGit(_args.SolutionDir);
                _args.ReleaseProjects.ForEachParallel(csproj => UpdateVersion(csproj, version));

                Log.Debug($"Looking for Solution files in {_args.SolutionDir}");
                var solutionFiles = _args.SolutionDir.EnumerateFiles("*.sln").ToList();
                Log.Debug($"Found:");
                foreach (var solutionFile in solutionFiles)
                    Log.Debug(solutionFile.Name);

                foreach (var solutionFile in solutionFiles)
                {
                    Log.Information($"Building: {solutionFile.FullName()}");
                    var result = ExternalProcess.Run("dotnet", $" build \"{solutionFile.FullName()}\" --configuration {_args.Configuration} --no-incremental --verbosity quiet", Log.Debug, Log.Error);
                    if (result.ExitCode != 0)
                        throw new BuildException($"Build failed for {solutionFile.FullName()}. See logs for details");
                }
            }
            finally
            {
                _args.ReleaseProjects.ForEachParallel(RevertVersion);
            }
        }

        private static SemVersion GetVersionFromGit(DirPath solutionDir)
        {
            var gitPath = solutionDir.ToDir(".git").FullName();
            Log.Debug($"Trying to resolve version from git in {gitPath}");

            var gitVersions = PowerShellCli.RunScript($"git --git-dir=\"{gitPath}\" tag -l v*");

            if (gitVersions.Any() == false)
            {
                Log.Debug($"No tags found in gitrepo in {gitPath}");
                return new SemVersion(0, 0, 0);
            }

            var latestVersion = gitVersions.Select(v => new SemVersion(v)).Max();
            var shortHash = PowerShellCli.RunScript($"git --git-dir=\"{gitPath }\" log --pretty=format:'%h' -n 1").First().ToString();

            latestVersion.Metadata += shortHash;
            Log.Debug($"Version resolved from git to {latestVersion}");
            return latestVersion;
        }

        private static void UpdateVersion(FilePath projectFile, SemVersion version)
        {
            var tmpFile = GetTempFilePath(projectFile);
            Log.Debug($"Backing up {projectFile.FullName()} to {tmpFile.FullName()}");
            projectFile.CopyTo(tmpFile, overwrite: true);

            var projectXml = projectFile.ReadAllText();
            var projectXDoc = XDocument.Parse(projectXml);
            var propertyGroupElement = projectXDoc.Root.XPathSelectElement("//Project/PropertyGroup");
            if (propertyGroupElement == null)
                throw new BuildException($"Failed to update version for {projectFile.FullName()}. Csproj file format seems to be wrong");

            EnsureNodeWithValue(propertyGroupElement, "Version", version.SemVer20String);

            EnsureNodeWithValue(propertyGroupElement, "AssemblyVersion", version.SemVer10String);
            EnsureNodeWithValue(propertyGroupElement, "FileVersion", version.SemVer10String);
            Log.Debug($"Updating {projectFile.Name} to {version.SemVer20String}");
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

        private static void RevertVersion(FilePath projectFile)
        {
            var tmpFile = GetTempFilePath(projectFile);
            if (tmpFile.Exists() == false)
            {
                Log.Debug($"{tmpFile.FullName()} not found.");
            }
            else
            {
                Log.Debug($"Reverting {tmpFile.FullName()} to {projectFile.FullName()}");
                tmpFile.MoveTo(projectFile, overwrite: true, ensureTargetDir: false);
            }
        }

        private static FilePath GetTempFilePath(FilePath projectFile)
        {
            return (projectFile.FullName() + _tempFileSuffix).ToFile();
        }
    }
}
