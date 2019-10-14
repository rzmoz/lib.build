using System.Threading.Tasks;
using DotNet.Basics.Collections;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public class SolutionTest : BuildStep
    {
        protected override Task<int> InnerRunAsync(BuildArgs args, ILogDispatcher log)
        {
            if (args.TestProjects.None())
            {
                log.Info($"No test projects found. Skipping testing...");
                return Task.FromResult(0);
            }

            if (string.IsNullOrWhiteSpace(args.TestFilter) == false)
                log.Info($"with test filter: {args.TestFilter}");

            if (args.WithTestResults)
                InitDir(args.TestResultsDir, log);

            var exitCode = 0;

            args.TestProjects.ForEachParallel(testProj =>
            {
                var projectLog = log.InContext(testProj.NameWoExtension);
                var testAction = $" test \"{testProj.FullName()}\" --configuration {args.Configuration} --no-build --no-restore";
                if (args.WithTestResults)
                    testAction +=$" --logger \"trx;LogFileName={args.TestResultsDir.ToFile($"{testProj.NameWoExtension}.results.xml")}\"";

                if (string.IsNullOrWhiteSpace(args.TestFilter) == false)
                    testAction += $" --filter \"{args.TestFilter}\"";

                exitCode += ExternalProcess.Run("dotnet", testAction, projectLog.Verbose, projectLog.Error);
            });
            return Task.FromResult(exitCode);
        }
    }
}
