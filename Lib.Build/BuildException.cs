using System;
using System.Runtime.Serialization;

namespace Lib.Build
{
    public class BuildException : Exception
    {
        public BuildException()
        {
        }

        protected BuildException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public BuildException(string message) : base(message)
        {
        }

        public BuildException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
