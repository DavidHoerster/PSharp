﻿using System;
using Microsoft.PSharp;

namespace PingPong
{
    internal class Client : Machine
    {
        private MachineId Server;
        private int Counter;

        [Start]
        [OnEntry(nameof(InitOnEntry))]
        [OnEventGotoState(typeof(Unit), typeof(Active))]
        class Init : MachineState { }

        void InitOnEntry()
        {
            this.Server = (this.ReceivedEvent as Config).Id;
            this.Counter = 0;
            this.Raise(new Unit());
        }

        [OnEntry(nameof(ActiveOnEntry))]
        [OnEventGotoState(typeof(Unit), typeof(Active))]
        [OnEventDoAction(typeof(Pong), nameof(SendPing))]
        class Active : MachineState { }

        void ActiveOnEntry()
        {
            if (this.Counter == 5)
            {
                this.Raise(new Halt());
            }
        }

        private void SendPing()
        {
            this.Counter++;
            Console.WriteLine("\nTurns: {0} / 5\n", this.Counter);
            this.RemoteSend(this.Server, new Ping());
            this.Raise(new Unit());
        }
    }
}
