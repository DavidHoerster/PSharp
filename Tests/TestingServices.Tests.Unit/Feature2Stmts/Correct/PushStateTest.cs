﻿//-----------------------------------------------------------------------
// <copyright file="PushStateTest.cs">
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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PSharp.TestingServices.Tests.Unit
{
    [TestClass]
    public class PushStateTest
    {
        class A : Machine
        {
            [Start]
            [OnEventDoAction(typeof(E1), nameof(foo))]
            [OnEventPushState(typeof(E2), typeof(S1))]
            class S0 : MachineState { }

            [OnEventDoAction(typeof(E3), nameof(bar))]
            class S1 : MachineState { }

            void foo()
            {
            }

            void bar()
            {
                this.Pop();
            }

        }

        class E1 : Event
        { }
        class E2 : Event
        { }
        class E3 : Event
        { }
        class E4 : Event
        { }

        class B : Machine
        {
            [Start]
            [OnEntry(nameof(Conf))]
            class Init : MachineState { }

            void Conf()
            {
                var a = this.CreateMachine(typeof(A));
                this.Send(a, new E2()); // push(S1)
                this.Send(a, new E1()); // execute foo without popping
                this.Send(a, new E3()); // can handle it because A is still in S1
            }

        }

        public static class TestProgram
        {
            [Test]
            public static void Execute(PSharpRuntime runtime)
            {
                runtime.CreateMachine(typeof(B));
            }
        }

        [TestMethod]
        public void TestPushStateEvent()
        {
            var configuration = Configuration.Create();
            configuration.SuppressTrace = true;
            configuration.Verbose = 2;

            var engine = TestingEngineFactory.CreateBugFindingEngine(
                configuration, TestProgram.Execute);
            engine.Run();

            Assert.AreEqual(0, engine.TestReport.NumOfFoundBugs);
        }
    }
}
