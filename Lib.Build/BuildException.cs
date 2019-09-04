using System;
using System.Runtime.Serialization;
using DotNet.Basics.Cli;

namespace Lib.Build
{
    public class BuildException : CliException
    {
        public BuildException() : base(LogOptions.ExcludeStackTrace)
        {
        }

        protected BuildException(SerializationInfo info, StreamingContext context) : base(info, context, LogOptions.ExcludeStackTrace)
        {
        }

        public BuildException(string message) : base(message, LogOptions.ExcludeStackTrace)
        {
        }

        public BuildException(string message, Exception innerException) : base(message, innerException, LogOptions.ExcludeStackTrace)
        {
        }
    }
}
