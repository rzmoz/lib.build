using DotNet.Basics.Cli;

namespace Lib.Build
{
    public class BuildException : CliException
    {
        public BuildException(string message, int exitCode) : base(message, LogOptions.ExcludeStackTrace)
        {
            ExitCode = exitCode;
        }

        public int ExitCode { get; set; }
    }
}
