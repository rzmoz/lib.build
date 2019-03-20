using System.Linq;
using System.Threading.Tasks;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;
using Serilog;

namespace Lib.Build
{
    public class SolutionBuild
    {
        private readonly BuildArgs _args;

        public SolutionBuild(BuildArgs args)
        {
            _args = args;
        }

        public Task RunAsync()
        {
            Log.Debug($"Looking for Solution files in {_args.SolutionDir}");

            var solutionFiles = _args.SolutionDir.EnumerateFiles("*.sln").ToList();
            foreach (var solutionFile in solutionFiles)
            {
                Log.Information("Building: {SolutionFile}", solutionFile.FullName());
                var result = ExternalProcess.Run("dotnet",$" build \"{solutionFile.FullName()}\" -configuration {_args.Configuration} --no-incremental --verbosity quiet" , Log.Debug, Log.Error);
                if (result.ExitCode != 0)
                    throw new BuildException($"Build failed for {solutionFile.FullName()}. See logs for details");
            }

            return Task.CompletedTask;
        }
    }
}
