using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using DotNet.Basics.Collections;
using DotNet.Basics.IO;
using DotNet.Basics.PowerShell;
using DotNet.Basics.Sys;
using Serilog;

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

        public async Task RunAsync()
        {
            Log.Debug($"Looking for Solution files in {_args.SolutionDir}");

            try
            {
                var version = _args.Version;

                var shortHash = PowerShellCli.RunScript($"git --git-dir=\"{_args.SolutionDir.FullName()}.git\" log --pretty=format:'%h' -n 1").First().ToString();
                Log.Debug($"Adding short hash to version metadata:: {shortHash}");
                version.Metadata += shortHash;
                Log.Debug($"Updating projects with version: {version.SemVer20String}");

                await _args.ReleaseProjects.ForEachParallelAsync(csproj => UpdateVersion(csproj, version)).ConfigureAwait(false);

                var solutionFiles = _args.SolutionDir.EnumerateFiles("*.sln").ToList();
                foreach (var solutionFile in solutionFiles)
                {
                    Log.Information("Building: {SolutionFile}", solutionFile.FullName());
                    var result = ExternalProcess.Run("dotnet", $" build \"{solutionFile.FullName()}\" --configuration {_args.Configuration} --no-incremental --verbosity quiet", Log.Debug, Log.Error);
                    if (result.ExitCode != 0)
                        throw new BuildException($"Build failed for {solutionFile.FullName()}. See logs for details");
                }
            }
            finally
            {
                await _args.ReleaseProjects.ForEachParallelAsync(RevertVersion).ConfigureAwait(false);
            }
        }

        private static Task UpdateVersion(FilePath projectFile, SemVersion version)
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

            Log.Verbose($"Updated csproj for {projectFile.Name}: \r\n{projectXDoc}");

            using (var writer = new StreamWriter(projectFile.FullName()))
            using (var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings { Indent = true }))
            {
                projectXDoc.Save(xmlWriter);
            }

            return Task.CompletedTask;
        }

        private static void EnsureNodeWithValue(XElement parentNode, string nodeName, string value)
        {
            var node = parentNode.XPathSelectElement($"//{nodeName}");
            if (node == null)
                parentNode.Add(new XElement(nodeName, value));
            else
                node.Value = value;
        }

        private static Task RevertVersion(FilePath projectFile)
        {
            var tmpFile = GetTempFilePath(projectFile);
            Log.Debug($"Reverting {tmpFile.FullName()} to {projectFile.FullName()}");
            tmpFile.MoveTo(projectFile, overwrite: true, ensureTargetDir: false);
            return Task.CompletedTask;
        }

        private static FilePath GetTempFilePath(FilePath projectFile)
        {
            return (projectFile.FullName() + _tempFileSuffix).ToFile();
        }
    }
}
