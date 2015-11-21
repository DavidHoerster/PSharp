﻿//-----------------------------------------------------------------------
// <copyright file="SEMTwoMachines2Test.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
//      EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
//      OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
// ----------------------------------------------------------------------------------
//      The example companies, organizations, products, domain names,
//      e-mail addresses, logos, people, places, and events depicted
//      herein are fictitious.  No association with any real company,
//      organization, product, domain name, email address, logo, person,
//      places, or events is intended or should be inferred.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.PSharp.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PSharp.SystematicTesting.Tests.Unit
{
    [TestClass]
    public class SEMTwoMachines2Test : BasePSharpTest
    {
        class Ping : Event
        {
            public MachineId Id;
            public Ping(MachineId id) : base(1, -1) { this.Id = id; }
        }

        class Pong : Event
        {
            public Pong() : base(1, -1) { }
        }

        class Success : Event { }
        class PingIgnored : Event { }
        class PongHalted : Event { }

        class PING : Machine
        {
            MachineId PongId;
            int Count;

            [Start]
            [OnEntry(nameof(EntryInit))]
            [OnEventGotoState(typeof(Success), typeof(SendPing))]
            class Init : MachineState { }

            void EntryInit()
            {
                PongId = this.CreateMachine(typeof(PONG));
                this.Raise(new Success());
            }

            [OnEntry(nameof(EntrySendPing))]
            [OnEventGotoState(typeof(Success), typeof(WaitPong))]
            class SendPing : MachineState { }

            void EntrySendPing()
            {
                Count = Count + 1;
                if (Count == 1)
                {
                    this.Send(PongId, new Ping(this.Id));
                }
                // halt PONG after one exchange
                if (Count == 2)
                {
                    //this.Send(PongId, new Halt());
                    this.Send(PongId, new PingIgnored());
                }

                this.Raise(new Success());
            }

            [OnEventGotoState(typeof(Pong), typeof(SendPing))]
            class WaitPong : MachineState { }

            class Done : MachineState { }
        }

        class PONG : Machine
        {
            [Start]
            [OnEventGotoState(typeof(Ping), typeof(SendPong))]
            [OnEventDoAction(typeof(PingIgnored), nameof(Action1))]
            class WaitPing : MachineState { }

            void Action1()
            {
                this.Assert(false); // reachable
            }

            [OnEntry(nameof(EntrySendPong))]
            [OnEventGotoState(typeof(Success), typeof(WaitPing))]
            class SendPong : MachineState { }

            void EntrySendPong()
            {
                this.Send((this.ReceivedEvent as Ping).Id, new Pong());
                this.Raise(new Success());
            }
        }

        public static class TestProgram
        {
            [Test]
            public static void Execute(PSharpRuntime runtime)
            {
                runtime.CreateMachine(typeof(PING));
            }
        }

        /// <summary>
        /// Tests that an event sent to a machine after it received the
        /// "halt" event is ignored by the halted machine.
        /// </summary>
        [TestMethod]
        public void TestEventSentAfterSentHalt()
        {
            var configuration = Configuration.Create();
            configuration.SuppressTrace = true;
            configuration.Verbose = 2;
            configuration.SchedulingStrategy = SchedulingStrategy.DFS;

            var engine = SCTEngine.Create(configuration, TestProgram.Execute).Run();
            Assert.AreEqual(1, engine.NumOfFoundBugs);
        }
    }
}
