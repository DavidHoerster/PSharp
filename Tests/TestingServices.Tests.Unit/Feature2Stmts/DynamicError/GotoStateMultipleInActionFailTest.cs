﻿//-----------------------------------------------------------------------
// <copyright file="GotoStateMultipleInActionFailTest.cs">
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
    public class GotoStateTopLevelActionFailTest
    {
        public enum ErrorType { CALL_GOTO, CALL_RAISE, CALL_SEND, ON_EXIT };
        public static ErrorType ErrorTypeVal = ErrorType.CALL_GOTO;

        class E : Event { }

        class Program : Machine
        {
            [Start]
            [OnEntry(nameof(EntryInit))]
            [OnExit(nameof(ExitMethod))]
            class Init : MachineState { }

            async Task EntryInit()
            {
                this.Foo();
                switch(ErrorTypeVal)
                {
                    case ErrorType.CALL_GOTO:
                        this.Goto(typeof(Done));
                        break;
                    case ErrorType.CALL_RAISE:
                        this.Raise(new E());
                        break;
                    case ErrorType.CALL_SEND:
                        await this.Send(Id, new E());
                        break;
                    case ErrorType.ON_EXIT:
                        break;
                }
            }

            void ExitMethod()
            {
                this.Pop();
            }

            void Foo()
            {
                this.Goto(typeof(Done));
            }

            class Done : MachineState { }
        }

        public static class TestProgram
        {
            [Test]
            public static async Task Execute1(IPSharpRuntime runtime)
            {
                ErrorTypeVal = ErrorType.CALL_GOTO;
                await runtime.CreateMachineAsync(typeof(Program));
            }

            [Test]
            public static async Task Execute2(IPSharpRuntime runtime)
            {
                ErrorTypeVal = ErrorType.CALL_RAISE;
                await runtime.CreateMachineAsync(typeof(Program));
            }

            [Test]
            public static async Task Execute3(IPSharpRuntime runtime)
            {
                ErrorTypeVal = ErrorType.CALL_SEND;
                await runtime.CreateMachineAsync(typeof(Program));
            }

            [Test]
            public static async Task Execute4(IPSharpRuntime runtime)
            {
                ErrorTypeVal = ErrorType.ON_EXIT;
                await runtime.CreateMachineAsync(typeof(Program));
            }
        }

        [TestMethod]
        public void TestGotoStateTopLevelActionFail1()
        {
            var configuration = Configuration.Create();
            configuration.SuppressTrace = true;
            configuration.Verbose = 2;

            var engine = TestingEngineFactory.CreateBugFindingEngine(
                configuration, TestProgram.Execute1);
            engine.Run();

            Assert.AreEqual(1, engine.TestReport.NumOfFoundBugs);

            var bugReport = "Machine 'Microsoft.PSharp.TestingServices.Tests.Unit.GotoStateTopLevelActionFailTest+Program(1)' " +
                "has called multiple raise/goto/pop in the same action.";
            Assert.IsTrue(engine.TestReport.BugReports.Count == 1);
            Assert.IsTrue(engine.TestReport.BugReports.Contains(bugReport));
        }

        [TestMethod]
        public void TestGotoStateTopLevelActionFail2()
        {
            var configuration = Configuration.Create();
            configuration.SuppressTrace = true;
            configuration.Verbose = 2;

            var engine = TestingEngineFactory.CreateBugFindingEngine(
                configuration, TestProgram.Execute2);
            engine.Run();

            var bugReport = "Machine 'Microsoft.PSharp.TestingServices.Tests.Unit.GotoStateTopLevelActionFailTest+Program(1)' " +
                "has called multiple raise/goto/pop in the same action.";
            Assert.IsTrue(engine.TestReport.BugReports.Count == 1);
            Assert.IsTrue(engine.TestReport.BugReports.Contains(bugReport));
        }

        [TestMethod]
        public void TestGotoStateTopLevelActionFail3()
        {
            var configuration = Configuration.Create();
            configuration.SuppressTrace = true;
            configuration.Verbose = 2;

            var engine = TestingEngineFactory.CreateBugFindingEngine(
                configuration, TestProgram.Execute3);
            engine.Run();

            Assert.AreEqual(1, engine.TestReport.NumOfFoundBugs);

            var bugReport = "Machine 'Microsoft.PSharp.TestingServices.Tests.Unit.GotoStateTopLevelActionFailTest+Program(1)' " +
                "cannot call API 'Send' after calling raise/goto/pop in the same action.";
            Assert.IsTrue(engine.TestReport.BugReports.Count == 1);
            Assert.IsTrue(engine.TestReport.BugReports.Contains(bugReport));
        }

        [TestMethod]
        public void TestGotoStateTopLevelActionFail4()
        {
            var configuration = Configuration.Create();
            configuration.SuppressTrace = true;
            configuration.Verbose = 2;

            var engine = TestingEngineFactory.CreateBugFindingEngine(
                configuration, TestProgram.Execute4);
            engine.Run();

            Assert.AreEqual(1, engine.TestReport.NumOfFoundBugs);

            var bugReport = "Machine 'Microsoft.PSharp.TestingServices.Tests.Unit.GotoStateTopLevelActionFailTest+Program(1)' " +
                "has called raise/goto/pop inside an OnExit method.";
            Assert.IsTrue(engine.TestReport.BugReports.Count == 1);
            Assert.IsTrue(engine.TestReport.BugReports.Contains(bugReport));
        }

    }
}