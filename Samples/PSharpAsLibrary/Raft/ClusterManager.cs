﻿using System;
using System.Threading.Tasks;

using Microsoft.PSharp;

namespace Raft
{
    internal class ClusterManager : Machine
    {
        #region events

        internal class NotifyLeaderUpdate : Event
        {
            public MachineId Leader;
            public int Term;

            public NotifyLeaderUpdate(MachineId leader, int term)
                : base()
            {
                this.Leader = leader;
                this.Term = term;
            }
        }

        internal class RedirectRequest : Event
        {
            public Event Request;

            public RedirectRequest(Event request)
                : base()
            {
                this.Request = request;
            }
        }

        internal class ShutDown : Event { }
        private class LocalEvent : Event { }

        #endregion

        #region fields

        MachineId[] Servers;
        int NumberOfServers;

        MachineId Leader;
        int LeaderTerm;

        MachineId Client;

        #endregion

        #region states

        [Start]
        [OnEntry(nameof(EntryOnInit))]
        [OnEventGotoState(typeof(LocalEvent), typeof(Configuring))]
        class Init : MachineState { }

        async Task EntryOnInit()
        {
            this.NumberOfServers = 5;
            this.LeaderTerm = 0;
            
            this.Servers = new MachineId[this.NumberOfServers];

            for (int idx = 0; idx < this.NumberOfServers; idx++)
            {
                this.Servers[idx] = await this.CreateMachine(typeof(Server));
            }

            this.Client = await this.CreateMachine(typeof(Client));
            this.Raise(new LocalEvent());
        }

        [OnEntry(nameof(ConfiguringOnInit))]
        [OnEventGotoState(typeof(LocalEvent), typeof(Availability.Unavailable))]
        class Configuring : MachineState { }

        async Task ConfiguringOnInit()
        {
            for (int idx = 0; idx < this.NumberOfServers; idx++)
            {
                await this.Send(this.Servers[idx], new Server.ConfigureEvent(idx, this.Servers, this.Id));
            }

            await this.Send(this.Client, new Client.ConfigureEvent(this.Id));
            this.Raise(new LocalEvent());
        }

        class Availability : StateGroup
        {
            [OnEventDoAction(typeof(NotifyLeaderUpdate), nameof(BecomeAvailable))]
            [OnEventDoAction(typeof(ShutDown), nameof(ShuttingDown))]
            [OnEventGotoState(typeof(LocalEvent), typeof(Available))]
            [DeferEvents(typeof(Client.Request))]
            public class Unavailable : MachineState { }


            [OnEventDoAction(typeof(Client.Request), nameof(SendClientRequestToLeader))]
            [OnEventDoAction(typeof(RedirectRequest), nameof(RedirectClientRequest))]
            [OnEventDoAction(typeof(NotifyLeaderUpdate), nameof(RefreshLeader))]
            [OnEventDoAction(typeof(ShutDown), nameof(ShuttingDown))]
            [OnEventGotoState(typeof(LocalEvent), typeof(Unavailable))]
            public class Available : MachineState { }
        }

        void BecomeAvailable()
        {
            this.UpdateLeader(this.ReceivedEvent as NotifyLeaderUpdate);
            this.Raise(new LocalEvent());
        }


        async Task SendClientRequestToLeader()
        {
            await this.Send(this.Leader, this.ReceivedEvent);
        }

        async Task RedirectClientRequest()
        {
            await this.Send(this.Id, (this.ReceivedEvent as RedirectRequest).Request);
        }
        
        void RefreshLeader()
        {
            this.UpdateLeader(this.ReceivedEvent as NotifyLeaderUpdate);
        }

        void BecomeUnavailable() { }

        async Task ShuttingDown()
        {
            for (int idx = 0; idx < this.NumberOfServers; idx++)
            {
                await this.Send(this.Servers[idx], new Server.ShutDown());
            }

            this.Raise(new Halt());
        }

        #endregion

        #region core methods

        /// <summary>
        /// Updates the leader.
        /// </summary>
        /// <param name="request">NotifyLeaderUpdate</param>
        void UpdateLeader(NotifyLeaderUpdate request)
        {
            if (this.LeaderTerm < request.Term)
            {
                this.Leader = request.Leader;
                this.LeaderTerm = request.Term;
            }
        }

        #endregion
    }
}
