using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;

namespace Lib.Build
{
    public class SolutionPostBuild
    {
        private readonly BuildArgs _args;
        private readonly CallbackRunner _callbackRunner;

        private const string _dotNetFrameworkPattern = @"^net[0-9]+$";
        private static readonly Regex _dotNetFrameworkRegex = new Regex(_dotNetFrameworkPattern, RegexOptions.IgnoreCase);
        private readonly ILogDispatcher _slnLog;

        private static readonly IReadOnlyList<string> _languageDirs = new[]
        {
            "cs",
            "de",
            "es",
            "fr",
            "it",
            "ja",
            "ko",
            "pl",
            "pt-BR",
            "ru",
            "tr",
            "zh-Hans",
            "zh-Hant"
        };

        public SolutionPostBuild(BuildArgs args, ILogDispatcher slnLog, CallbackRunner callbackRunner)
        {
            _args = args;
            _callbackRunner = callbackRunner;
            _slnLog = slnLog.InContext(nameof(SolutionPostBuild));
        }

        public async Task RunAsync()
        {
            _slnLog.Information($"Starting {nameof(SolutionPostBuild)}");
            _args.ReleaseProjects.ForEachParallel(CopyArtifacts);
            _args.ReleaseProjects.ForEachParallel(CleanRuntimeArtifacts);

            await _callbackRunner.InvokeCallbacksAsync(_args.PostBuildCallbacks, _args.SolutionDir, _args.ReleaseArtifactsDir, _slnLog).ConfigureAwait(false);
        }

        private DirPath GetTargetDir(FilePath projectFile)
        {
            return _args.ReleaseArtifactsDir.ToDir(projectFile.NameWoExtension);
        }

        private DirPath GetArtifactsSourceDir(FilePath projectFile)
        {
            var sourceDir = projectFile.Directory().ToDir("bin");
            if (sourceDir.Add(_args.Configuration).Exists())
                sourceDir = sourceDir.Add(_args.Configuration);
            try
            {
                sourceDir = sourceDir.EnumerateDirectories().SingleOrDefault() ?? sourceDir;//look for target framework dir
            }
            catch (InvalidOperationException)
            {
                //ignore if there's more than a single file.
            }

            if (_args.Publish && sourceDir.Add(nameof(_args.Publish)).Exists())
                sourceDir = sourceDir.Add(nameof(_args.Publish));

            _slnLog.Verbose($"{projectFile.NameWoExtension.Highlight()} source dir resolve to: {sourceDir.FullName()}");

            return sourceDir;
        }

        private void CleanRuntimeArtifacts(FilePath projectFile)
        {
            var targetDir = GetTargetDir(projectFile);
            _languageDirs.ForEachParallel(dir => targetDir.ToDir(dir).DeleteIfExists());
        }

        private void CopyArtifacts(FilePath projectFile)
        {
            var releaseTargetDir = GetTargetDir(projectFile);

            _slnLog.Debug($"Copying build artifacts for {releaseTargetDir.Name.Highlight()} to {releaseTargetDir.FullName()}");

            var artifactsSourceDir = GetArtifactsSourceDir(projectFile);

            var projectLog = _slnLog.InContext(projectFile.NameWoExtension);

            if (_dotNetFrameworkRegex.IsMatch(artifactsSourceDir.Name))
            {
                _slnLog.Debug($"{projectFile.NameWoExtension.Highlight()} is .NET Framework");
                var binDir = artifactsSourceDir.Add("bin");

                artifactsSourceDir.EnumerateFiles("*.dll").MoveTo(binDir, true, true);
                artifactsSourceDir.EnumerateFiles("*.pdb").MoveTo(binDir, true, true);
            }
            else
                _slnLog.Debug($"{projectFile.NameWoExtension.Highlight()} is .NET Core");

            AssertWebJob(projectFile.NameWoExtension, artifactsSourceDir, projectLog);

            try
            {
                artifactsSourceDir.CopyTo(releaseTargetDir, true);
            }
            catch (Exception)
            {
                throw new BuildException($"Copy artifacts for {projectFile.Name} failed with");
            }
        }

        private void AssertWebJob(string projectName, DirPath outputDir, ILogDispatcher log)
        {
            var appSettingsFile = outputDir.ToFile("appSettings.json");

            if (appSettingsFile.Exists() == false)
            {
                log.Verbose($"Not WebJob. {appSettingsFile.FullName()} not found");
                return;
            }
            var appSettingsRawContent = appSettingsFile.ReadAllText(IfNotExists.Mute).ToLowerInvariant();//lowercase to ignore case when accessing properties
            log.Debug($"{appSettingsFile.Name} found:\r\n{appSettingsRawContent}");

            var appSettingsJson = JToken.Parse(appSettingsRawContent);
            var webJob = appSettingsJson["webjob"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(webJob))
            {
                log.Verbose($"Not WebJob. Property 'webjob' not found in {appSettingsFile.FullName()}");
                return;
            }
            log.Information($"WebJob found: {webJob}");

            var webJobTargetDir = outputDir.Add("app_data", "jobs", webJob, projectName);
            Robocopy.MoveContent(outputDir.FullName(), webJobTargetDir.FullName(), null, true, null, msg => _slnLog.Debug(msg), msg => _slnLog.Error(msg));

        }
    }
}
