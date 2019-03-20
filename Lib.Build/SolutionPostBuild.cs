using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lib.Build
{
    public class SolutionPostBuild
    {
        private readonly BuildArgs _args;

        public SolutionPostBuild(BuildArgs args)
        {
            _args = args;
        }

        public Task RunAsync()
        {
            return Task.CompletedTask;
        }
    }
}
