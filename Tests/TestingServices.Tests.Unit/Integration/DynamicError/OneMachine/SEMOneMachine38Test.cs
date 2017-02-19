﻿//-----------------------------------------------------------------------
// <copyright file="SEMOneMachine38Test.cs">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//      EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//      MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//      IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
//      CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//      TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//      SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.PSharp.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PSharp.TestingServices.Tests.Unit
{
    [TestClass]
    public class SEMOneMachine38Test
    {
        class E : Event { }

        class Program : Machine
        {
            int i;

            [Start]
            [OnEntry(nameof(EntryInit))]
            [OnExit(nameof(ExitInit))]
            [OnEventPushState(typeof(E), typeof(Call))]
            [OnEventDoAction(typeof(Default), nameof(InitAction))]
            class Init : MachineState { }

            void EntryInit()
            {
                i = 0;
                this.Raise(new E());
            }

            void ExitInit() { }

            void InitAction()
            {
                this.Assert(false); // reachable
            }

            [OnEntry(nameof(EntryCall))]
            [OnExit(nameof(ExitCall))]
            [IgnoreEvents(typeof(E))]
            class Call : MachineState { }

            void EntryCall()
            {
                if (i == 0)
                {
                    this.Raise(new E());
                }
                else
                {
                    i = i + 1;
                }
            }

            void ExitCall() { }
        }

        public static class TestProgram
        {
            [Test]
            public static void Execute(Runtime runtime)
            {
                runtime.CreateMachine(typeof(Program));
            }
        }

        /// <summary>
        /// P# semantics test: one machine: "null" handler semantics.
        /// Testing that null handler is inherited by the pushed state.
        /// </summary>
        [TestMethod]
        public void TestNullHandlerInheritedByPushTransition()
        {
            var configuration = Configuration.Create();
            configuration.SuppressTrace = true;
            configuration.Verbose = 2;

            var engine = TestingEngineFactory.CreateBugFindingEngine(
                configuration, TestProgram.Execute);
            engine.Run();

            Assert.AreEqual(1, engine.TestReport.NumOfFoundBugs);
        }
    }
}
