﻿using System;
using System.Collections.Generic;
using Microsoft.PSharp;

namespace PBoundedAsync
{
    public class Test
    {
        static void Main(string[] args)
        {
            Runtime.RegisterNewEvent(typeof(unit));
            Runtime.RegisterNewEvent(typeof(req));
            Runtime.RegisterNewEvent(typeof(resp));
            Runtime.RegisterNewEvent(typeof(init));
            Runtime.RegisterNewEvent(typeof(myCount));

            Runtime.RegisterNewMachine(typeof(Scheduler));
            Runtime.RegisterNewMachine(typeof(Process));

            Runtime.Start();
            Runtime.Wait();
            Runtime.Dispose();
        }
    }
}
