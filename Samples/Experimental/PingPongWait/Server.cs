﻿using System;
using System.Threading.Tasks;

using Microsoft.PSharp;

namespace PingPong
{
    internal class Server : Machine
    {
        MachineId Client;

		[Start]
        [OnEntry(nameof(InitOnEntry))]
        [OnEventGotoState(typeof(Unit), typeof(Active))]
        class Init : MachineState { }

		async Task InitOnEntry()
        {
            this.Client = await this.CreateMachine(typeof(Client));
            await this.Send(this.Client, new Config(this.Id));
            this.Raise(new Unit());
        }

        [OnEntry(nameof(ActiveOnEntry))]
        [OnEventDoAction(typeof(Pong), nameof(SendPing))]
        class Active : MachineState { }

        Task ActiveOnEntry()
        {
            this.SendPing();
			return this.DoneTask;
        }

        Task SendPing()
        {
            this.Send(this.Client, new Ping());
			return this.DoneTask;
        }
    }
}
