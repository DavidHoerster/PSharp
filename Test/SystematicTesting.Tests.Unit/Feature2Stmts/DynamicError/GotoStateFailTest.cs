﻿//-----------------------------------------------------------------------
// <copyright file="GotoStateFailTest.cs">
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

namespace Microsoft.PSharp.SystematicTesting.Tests.Unit
{
    [TestClass]
    public class GotoStateFailTest
    {
        class Program : Machine
        {
            [Start]
            [OnEntry(nameof(EntryInit))]
            class Init : MachineState { }

            void EntryInit()
            {
                this.Goto(typeof(object));
            }

            class Done : MachineState { }
        }

        public static class TestProgram
        {
            [Test]
            public static void Execute(PSharpRuntime runtime)
            {
                runtime.CreateMachine(typeof(Program));
            }
        }

        [TestMethod]
        public void TestGotoStateFail()
        {
            var configuration = Configuration.Create();
            configuration.SuppressTrace = true;
            configuration.Verbose = 2;

            var engine = TestingEngine.Create(configuration, TestProgram.Execute).Run();
            Assert.AreEqual(1, engine.NumOfFoundBugs);
        }
    }
}
