using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class SolutionBuild : BuildStep
    {
        private const string _tempFileSuffix = ".temp";

        protected override Task<int> InnerRunAsync(BuildArgs args, ILogDispatcher log)
        {
            log.Info($"Starting {nameof(SolutionBuild)}");

            try
            {
                args.ReleaseProjects.ForEachParallel(csproj => PatchVersion(csproj, args.Version, log));

                log.Verbose($"Looking for Solution files in {args.SolutionDir}");
                var solutionFiles = args.SolutionDir.EnumerateFiles("*.sln").ToList();
                log.Debug($"Solution Found: {solutionFiles.Select(sln => sln.Name).JoinString().Highlight()}");

                foreach (var solutionFile in solutionFiles)
                {
                    var publishAction = $"publish \"{solutionFile.FullName()}\" --configuration {args.Configuration} --force --verbosity quiet";
                    var buildAction = $" build \"{solutionFile.FullName()}\" --configuration {args.Configuration} --no-incremental --verbosity quiet";
                    var action = args.Publish ? publishAction : buildAction;

                    log.Info(action.Highlight());

                    var exitCode = ExternalProcess.Run("dotnet", action, log.Debug, log.Error);
                    if (exitCode != 0)
                        throw new BuildException($"Build failed for {solutionFile.FullName()}. See logs for details", 400);
                }

                return Task.FromResult(0);
            }
            finally
            {
                args.ReleaseProjects.ForEachParallel(csproj => RevertVersion(csproj, log));
            }
        }

        private void PatchVersion(FilePath projectFile, SemVersion version, ILogDispatcher log)
        {
            var tmpFile = GetTempFilePath(projectFile);
            log.Verbose($"Backing up {projectFile.FullName()} to {tmpFile.FullName()}");
            projectFile.CopyTo(tmpFile, overwrite: true);

            var projectXml = projectFile.ReadAllText();
            var projectXDoc = XDocument.Parse(projectXml);
            var propertyGroupElement = projectXDoc.Root.XPathSelectElement("//Project/PropertyGroup");
            if (propertyGroupElement == null)
                throw new BuildException($"Failed to update version for {projectFile.FullName()}. Csproj file format seems to be wrong", 400);

            EnsureNodeWithValue(propertyGroupElement, "Version", version.SemVer20String);
            EnsureNodeWithValue(propertyGroupElement, "AssemblyVersion", version.FileVerString);
            EnsureNodeWithValue(propertyGroupElement, "FileVersion", version.FileVerString);
            log.Debug($"Patching {projectFile.Name.Highlight()} with version {version.SemVer20String.Highlight()}");
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

        private void RevertVersion(FilePath projectFile, ILogDispatcher log)
        {
            var tmpFile = GetTempFilePath(projectFile);
            if (tmpFile.Exists() == false)
            {
                log.Debug($"{tmpFile.FullName()} not found.");
            }
            else
            {
                log.Debug($"Reverting {tmpFile.FullName()} to {projectFile.FullName()}");
                tmpFile.MoveTo(projectFile, overwrite: true, ensureTargetDir: false);
            }
        }

        private static FilePath GetTempFilePath(FilePath projectFile)
        {
            return (projectFile.FullName() + _tempFileSuffix).ToFile();
        }
    }
}
