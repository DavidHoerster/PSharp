﻿using System;
using System.Threading.Tasks;

using Microsoft.PSharp;

namespace Raft
{
    internal class Client : Machine
    {
        #region events
        
        /// <summary>
        /// Used to configure the client.
        /// </summary>
        public class ConfigureEvent : Event
        {
            public MachineId Cluster;

            public ConfigureEvent(MachineId cluster)
                : base()
            {
                this.Cluster = cluster;
            }
        }

        /// <summary>
        /// Used for a client request.
        /// </summary>
        internal class Request : Event
        {
            public MachineId Client;
            public int Command;

            public Request(MachineId client, int command)
                : base()
            {
                this.Client = client;
                this.Command = command;
            }
        }

        internal class Response : Event { }

        private class LocalEvent : Event { }

        #endregion

        #region fields

        MachineId Cluster;
        
        int LatestCommand;
        int Counter;

        #endregion

        #region states

        [Start]
        [OnEntry(nameof(InitOnEntry))]
        [OnEventDoAction(typeof(ConfigureEvent), nameof(Configure))]
        [OnEventGotoState(typeof(LocalEvent), typeof(PumpRequest))]
        class Init : MachineState { }

		async Task InitOnEntry()
        {
            this.LatestCommand = -1;
            this.Counter = 0;
			await this.DoneTask;
        }

        async Task Configure()
        {
            this.Cluster = (this.ReceivedEvent as ConfigureEvent).Cluster;
            await this.Raise(new LocalEvent());
        }

        [OnEntry(nameof(PumpRequestOnEntry))]
        [OnEventDoAction(typeof(Response), nameof(ProcessResponse))]
        [OnEventGotoState(typeof(LocalEvent), typeof(PumpRequest))]
        class PumpRequest : MachineState { }

        async Task PumpRequestOnEntry()
        {
            this.LatestCommand = this.RandomInteger(100);
            this.Counter++;

            Console.WriteLine("\n [Client] new request " + this.LatestCommand + "\n");

			await this.Send(this.Cluster, new Request(this.Id, this.LatestCommand));
        }

        async Task ProcessResponse()
        {
            if (this.Counter == 3)
            {
                await this.Send(this.Cluster, new ClusterManager.ShutDown());
                await this.Raise(new Halt());
            }
            else
            {
                await this.Raise(new LocalEvent());
            }
        }

        #endregion
    }
}
