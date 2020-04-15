using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotNet.Basics.Cli;
using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;

namespace Lib.Build
{
    public abstract class CliArgsHydrator<T> : IArgsHydrator<T>
    {
        public abstract T Hydrate(ICliConfiguration config, T args, ILogger log = null);

        protected IReadOnlyList<FilePath> ResolveFiles(DirPath slnDir, string filter, string name, ILogger log)
        {
            log.Verbose($"Resolving {name.Highlight()} with filter: {filter.Highlight()}");
            try
            {
                return slnDir.EnumerateFiles(filter, SearchOption.AllDirectories).ToList(); ;
            }
            catch (IOException e)
            {
                log.Warning($"{e.GetType().Name.RemoveSuffix("Exception")}: {e.Message} when resolving {name.Highlight()} with {filter.Highlight()}");
                return new List<FilePath>();
            }
        }

        protected DirPath ResolveSolutionDir(ICliConfiguration config, ILogger log)
        {
            var slnDir = config["SolutionDir", 0]?.ToDir();

            if (slnDir == null || slnDir.Name.StartsWith("-"))
            {
                slnDir = ".".ToDir().FullName().ToDir();
                log.Info($"{"SolutionDir"} not set. Will try to resolve automatically.");
            }

            if (slnDir.Exists() == false)
                throw new CliException($"Directory not found: {slnDir.FullName()}", 404);
            do
            {
                log.Debug($"Looking for sln files in {slnDir.FullName().Highlight()}");
                if (slnDir.EnumerateFiles("*.sln").Any())
                {
                    log.Info($"SolutionDir resolved to: {slnDir.FullName().Highlight()}");
                    return slnDir;
                }

                slnDir = slnDir.Parent;
            } while (slnDir != null);

            throw new CliException($"No solution files found in {".".ToDir().FullName()} or any parent dir", 404);
        }
    }
}