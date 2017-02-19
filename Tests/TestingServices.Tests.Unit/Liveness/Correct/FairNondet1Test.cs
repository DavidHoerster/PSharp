﻿//-----------------------------------------------------------------------
// <copyright file="FairNondet1Test.cs">
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
    public class FairNondet1Test
    {
        class Unit : Event { }
        class UserEvent : Event { }
        class Done : Event { }
        class Loop : Event { }
        class Waiting : Event { }
        class Computing : Event { }

        class EventHandler : Machine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [OnEventGotoState(typeof(Unit), typeof(WaitForUser))]
            class Init : MachineState { }

            void InitOnEntry()
            {
                this.Raise(new Unit());
            }

            [OnEntry(nameof(WaitForUserOnEntry))]
            [OnEventGotoState(typeof(UserEvent), typeof(HandleEvent))]
            class WaitForUser : MachineState { }

            async Task WaitForUserOnEntry()
            {
                await this.Monitor<WatchDog>(new Waiting());
                await this.Send(this.Id, new UserEvent());
            }

            [OnEntry(nameof(HandleEventOnEntry))]
            [OnEventGotoState(typeof(Done), typeof(WaitForUser))]
            [OnEventGotoState(typeof(Loop), typeof(HandleEvent))]
            class HandleEvent : MachineState { }

            async Task HandleEventOnEntry()
            {
                await this.Monitor<WatchDog>(new Computing());
                if (this.FairRandom())
                {
                    await this.Send(this.Id, new Done());
                }
                else
                {
                    await this.Send(this.Id, new Loop());
                }
            }
        }

        class WatchDog : Monitor
        {
            [Start]
            [Cold]
            [OnEventGotoState(typeof(Waiting), typeof(CanGetUserInput))]
            [OnEventGotoState(typeof(Computing), typeof(CannotGetUserInput))]
            class CanGetUserInput : MonitorState { }

            [Hot]
            [OnEventGotoState(typeof(Waiting), typeof(CanGetUserInput))]
            [OnEventGotoState(typeof(Computing), typeof(CannotGetUserInput))]
            class CannotGetUserInput : MonitorState { }
        }

        public static class TestProgram
        {
            [Test]
            public static async Task Execute(IPSharpRuntime runtime)
            {
                await runtime.RegisterMonitorAsync(typeof(WatchDog));
                await runtime.CreateMachineAsync(typeof(EventHandler));
            }
        }

        [TestMethod]
        public void TestFairNondet1()
        {
            var configuration = Configuration.Create();
            configuration.SuppressTrace = true;
            configuration.Verbose = 3;
            configuration.CacheProgramState = true;
            configuration.LivenessTemperatureThreshold = 0;
            configuration.SchedulingStrategy = SchedulingStrategy.DFS;
            configuration.MaxSchedulingSteps = 300;

            IO.Debugging = true;

            var engine = TestingEngineFactory.CreateBugFindingEngine(
                configuration, TestProgram.Execute);
            engine.Run();
            
            Assert.AreEqual(0, engine.TestReport.NumOfFoundBugs);
        }
    }
}
