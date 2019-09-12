using System.Threading.Tasks;
using DotNet.Basics.Diagnostics;

namespace Lib.Build
{
    public class SolutionPostBuildCallBack : BuildStep
    {
        protected override Task<int> InnerRunAsync(BuildArgs args, ILogDispatcher log)
        {
            return InvokeCallbacksAsync(args.PostBuildCallbacks, args.SolutionDir, args.ReleaseArtifactsDir, log);
        }
    }
}
