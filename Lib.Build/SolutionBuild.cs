using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Basics.Cli;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class SolutionBuild : BuildStep
    {
        protected override Task<int> InnerRunAsync(BuildArgs args, ILogger log)
        {
            log.Info($"Starting {nameof(SolutionBuild)}");
            var dotnetAction = args.Publish ? "Publish" : "Build";

            foreach (var project in args.ReleaseProjects)
            {
                var actionBlock = $"{dotnetAction.ToLowerInvariant()} \"{project.FullName()}\" --configuration {args.Configuration} --verbosity quiet /p:Version={args.Version.SemVer20String}";

                if (args.Publish)
                    actionBlock += $" --runtime {args.Runtime}";
                else
                    actionBlock += $" --no-incremental";


                log.Info($"{dotnetAction}ing {project.NameWoExtension.Highlight()}");

                log.Info(actionBlock.Highlight());
                var exitCode = ExternalProcess.Run("dotnet", actionBlock, log.Debug, log.Error);
                if (exitCode != 0)
                    throw new CliException($"{dotnetAction} failed for {project.NameWoExtension.Highlight()}. See logs for details", 400);
            }

            return Task.FromResult(0);
        }
    }
}
