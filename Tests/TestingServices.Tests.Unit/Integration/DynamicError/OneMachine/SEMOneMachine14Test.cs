﻿//-----------------------------------------------------------------------
// <copyright file="SEMOneMachine14Test.cs">
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

using System.Threading.Tasks;

using Microsoft.PSharp.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PSharp.TestingServices.Tests.Unit
{
    [TestClass]
    public class SEMOneMachine14Test
    {
        class E1 : Event
        {
            public E1() : base(1, -1) { }
        }

        class E2 : Event
        {
            public E2() : base(1, -1) { }
        }

        class E3 : Event
        {
            public E3() : base(1, -1) { }
        }

        class Real1 : Machine
        {
            bool test = false;

            [Start]
            [OnEntry(nameof(EntryInit))]
            [OnEventGotoState(typeof(E1), typeof(S1))]
            [OnEventGotoState(typeof(E3), typeof(S2))]
            class Init : MachineState { }

            async Task EntryInit()
            {
                await this.Send(this.Id, new E1());
            }

            [OnEntry(nameof(EntryS1))]
            [OnExit(nameof(ExitS1))]
            [OnEventGotoState(typeof(E3), typeof(Init))]
            class S1 : MachineState { }

            async Task EntryS1()
            {
                test = true;
                await this.Send(this.Id, new E3());
            }

            async Task ExitS1()
            {
                await this.Send(this.Id, new E3());
            }

            [OnEntry(nameof(EntryS2))]
            class S2 : MachineState { }

            void EntryS2()
            {
                this.Assert(test == false); // reachable
            }
        }

        public static class TestProgram
        {
            [Test]
            public static async Task Execute(IPSharpRuntime runtime)
            {
                await runtime.CreateMachineAsync(typeof(Real1));
            }
        }

        /// <summary>
        /// P# semantics test: one machine, "goto" transition, action is not inherited
        /// by the destination state. This test checks that after "goto" transition,
        /// action of the src state is not inherited by the destination state.
        /// </summary>
        [TestMethod]
        public void TestGotoTransInheritance()
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
