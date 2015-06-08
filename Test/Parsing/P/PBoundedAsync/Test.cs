﻿using System;
using System.Collections.Generic;
using Microsoft.PSharp;

namespace PBoundedAsync
{
    public class Test
    {
        static void Main(string[] args)
        {
            Test.Execute();
        }

        [EntryPoint]
        public static void Execute()
        {
            Runtime.CreateMachine<Scheduler>();
            Runtime.WaitMachines();
        }
    }
}
