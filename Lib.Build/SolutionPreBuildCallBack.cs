using System.Threading.Tasks;
using DotNet.Basics.Diagnostics;

namespace Lib.Build
{
    public class SolutionPreBuildCallBack : BuildStep
    {
        protected override Task<int> InnerRunAsync(BuildArgs args, ILogDispatcher log)
        {
            return InvokeCallbacksAsync(args.PreBuildCallbacks, args.SolutionDir, args.ReleaseArtifactsDir, log);
        }
    }
}
