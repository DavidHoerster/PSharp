﻿//-----------------------------------------------------------------------
// <copyright file="SEMOneMachine32Test.cs" company="Microsoft">
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
    public class SEMOneMachine32Test
    {
        class E1 : Event
        {
            public E1() : base(1, -1) { }
        }

        class Real1 : Machine
        {
            bool test = false;

            [Start]
            [OnEntry(nameof(EntryInit))]
            [OnExit(nameof(ExitInit))]
            [OnEventPushState(typeof(Halt), typeof(S1))]
            [OnEventDoAction(typeof(E1), nameof(Action2))]
            class Init : MachineState { }

            void EntryInit()
            {
                this.Send(this.Id, new E1());
                this.Raise(new Halt());
            }

            void ExitInit() { }

            [OnEntry(nameof(EntryS1))]
            class S1 : MachineState { }

            void EntryS1()
            {
                test = true;
            }

            void Action2()
            {
                this.Assert(test == false); // reachable
            }
        }

        public static class TestProgram
        {
            [Test]
            public static void Execute(PSharpRuntime runtime)
            {
                runtime.CreateMachine(typeof(Real1));
            }
        }

        /// <summary>
        /// P# semantics test: one machine, "halt" is raised and handled.
        /// </summary>
        [TestMethod]
        public void TestRaiseHaltHandled()
        {
            var configuration = Configuration.Create();
            configuration.SuppressTrace = true;
            configuration.Verbose = 2;

            var engine = TestingEngine.Create(configuration, TestProgram.Execute).Run();
            Assert.AreEqual(1, engine.NumOfFoundBugs);
        }
    }
}
