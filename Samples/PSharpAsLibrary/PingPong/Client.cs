﻿using System;
using Microsoft.PSharp;

namespace PingPong
{
    internal class Client : Machine
    {
        private MachineId Server;
        private int Counter;

        [Start]
        [OnEventDoAction(typeof(Config), nameof(Configure))]
        class Init : MachineState { }

        void Configure()
        {
            this.Server = (this.ReceivedEvent as Config).Id;
            this.Counter = 0;
            this.Goto(typeof(Active));
        }

        [OnEntry(nameof(ActiveOnEntry))]
        [OnEventDoAction(typeof(Pong), nameof(SendPing))]
        class Active : MachineState { }

        void ActiveOnEntry()
        {
            if (this.Counter == 5)
            {
                this.Assert(false);
                this.Raise(new Halt());
            }
        }

        private void SendPing()
        {
            this.Counter++;
            Console.WriteLine("\nTurns: {0} / 5\n", this.Counter);
            this.Send(this.Server, new Ping());
            this.Goto(typeof(Active));
        }
    }
}
