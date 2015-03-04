﻿using System;
using System.Collections.Generic;
using Microsoft.PSharp;

namespace PingPong
{
    public class Test
    {
        static void Main(string[] args)
        {
            Runtime.RegisterNewEvent(typeof(Ping));
            Runtime.RegisterNewEvent(typeof(Pong));
            Runtime.RegisterNewEvent(typeof(Stop));
            Runtime.RegisterNewEvent(typeof(Unit));

            Runtime.RegisterNewMachine(typeof(Server));
            Runtime.RegisterNewMachine(typeof(Client));
            Runtime.Options.Verbose = true;

            Runtime.Start();
            Runtime.Wait();
            Runtime.Dispose();
        }
    }
}
