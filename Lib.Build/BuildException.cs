using System;
using System.Runtime.Serialization;
using DotNet.Basics.Cli;

namespace Lib.Build
{
    public class BuildException : CliException
    {
        public BuildException(int exitCode) : base(LogOptions.ExcludeStackTrace)
        {
            ExitCode = exitCode;
        }

        protected BuildException(SerializationInfo info, StreamingContext context, int exitCode) : base(info, context, LogOptions.ExcludeStackTrace)
        {
            ExitCode = exitCode;
        }

        public BuildException(string message, int exitCode) : base(message, LogOptions.ExcludeStackTrace)
        {
            ExitCode = exitCode;
        }

        public BuildException(string message, Exception innerException, int exitCode) : base(message, innerException, LogOptions.ExcludeStackTrace)
        {
            ExitCode = exitCode;
        }

        public int ExitCode { get; set; }
    }
}
