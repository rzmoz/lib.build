using System;
using System.Diagnostics;
using Serilog;

namespace Lib.Build
{
    class Program
    {
        static void Main(string[] args)
        {
            var buildArgs = new BuildArgs(args);

#if DEBUG
            if (buildArgs.IsDebug)
            {
                Log.Warning("Paused for debug. PID: {ProcessId} | Name: {ProcessName}. Press {ENTER} to continue..", Process.GetCurrentProcess().Id, Process.GetCurrentProcess().ProcessName, "[ENTER]");
                Console.ReadLine();
            }
#endif

            var solutionBuilder = new SolutionBuilder(buildArgs);
            solutionBuilder.Init();
        }
    }
}
