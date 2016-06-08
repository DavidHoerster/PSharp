﻿using System;
using System.Collections.Generic;

using Microsoft.PSharp;
using Microsoft.PSharp.TestingServices;
using Microsoft.PSharp.Utilities;

namespace NodeManager
{
    public class Test
    {
        static void Main(string[] args)
        {
            var configuration = Configuration.Create().
                WithLivenessCheckingEnabled().
                WithNumberOfIterations(10).
                WithVerbosityEnabled(2);
            TestingEngineFactory.CreateBugFindingEngine(configuration, Execute).Run();
        }

        [Microsoft.PSharp.Test]
        public static void Execute(PSharpRuntime runtime)
        {
            runtime.RegisterMonitor(typeof(M));
            runtime.CreateMachine(typeof(Environment));
        }
    }
}
