﻿using System;
using System.Collections.Generic;
using Microsoft.PSharp;

namespace BoundedAsync
{
    #region C# Classes and Structs

    internal struct CountMessage
    {
        public int Count;

        public CountMessage(int count)
        {
            this.Count = count;
        }
    }

    #endregion

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
