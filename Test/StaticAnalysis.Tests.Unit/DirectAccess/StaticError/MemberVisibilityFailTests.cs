﻿//-----------------------------------------------------------------------
// <copyright file="MemberVisibilityFailTests.cs">
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

using System;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.PSharp.LanguageServices.Compilation;
using Microsoft.PSharp.LanguageServices.Parsing;
using Microsoft.PSharp.Utilities;

namespace Microsoft.PSharp.StaticAnalysis.Tests.Unit
{
    [TestClass]
    public class MemberVisibilityFailTests : BasePSharpTest
    {
        [TestMethod, Timeout(3000)]
        public void TestPublicFieldVisibility()
        {
            var test = @"
using Microsoft.PSharp;

namespace Foo {
class M : Machine
{
 public int Num;

 [Start]
 [OnEntry(nameof(FirstOnEntryAction))]
 class First : MachineState { }

 void FirstOnEntryAction()
 {
  this.Num = 1;
 }
}
}";

            var solution = base.GetSolution(test);

            var configuration = Configuration.Create();
            configuration.ProjectName = "Test";
            configuration.Verbose = 2;

            IO.StartWritingToMemory();

            var context = CompilationContext.Create(configuration).LoadSolution(solution);

            ParsingEngine.Create(context).Run();
            RewritingEngine.Create(context).Run();
            StaticAnalysisEngine.Create(context).Run();

            var stats = AnalysisErrorReporter.GetStats();
            var expected = "... Static analysis detected '1' error";
            Assert.AreEqual(expected.Replace(Environment.NewLine, string.Empty), stats);

            var error = "Error: Field 'int Num' of machine 'M' is declared as 'public'.";
            Assert.AreEqual(error.Replace(Environment.NewLine, string.Empty),
                IO.GetOutput().Replace(Environment.NewLine, string.Empty));

            IO.StopWritingToMemory();
            AnalysisErrorReporter.ResetStats();
        }

        [TestMethod, Timeout(3000)]
        public void TestPublicMethodVisibility()
        {
            var test = @"
using Microsoft.PSharp;

namespace Foo {
class M : Machine
{
 int Num;

 [Start]
 [OnEntry(nameof(FirstOnEntryAction))]
 class First : MachineState { }

 public void FirstOnEntryAction()
 {
  this.Num = 1;
 }
}
}";

            var solution = base.GetSolution(test);

            var configuration = Configuration.Create();
            configuration.ProjectName = "Test";
            configuration.Verbose = 2;

            IO.StartWritingToMemory();

            var context = CompilationContext.Create(configuration).LoadSolution(solution);

            ParsingEngine.Create(context).Run();
            RewritingEngine.Create(context).Run();
            StaticAnalysisEngine.Create(context).Run();

            var stats = AnalysisErrorReporter.GetStats();
            var expected = "... Static analysis detected '1' error";
            Assert.AreEqual(expected.Replace(Environment.NewLine, string.Empty), stats);

            var error = "Error: Method 'FirstOnEntryAction' of machine 'M' is declared as 'public'.";
            Assert.AreEqual(error.Replace(Environment.NewLine, string.Empty),
                IO.GetOutput().Replace(Environment.NewLine, string.Empty));

            IO.StopWritingToMemory();
            AnalysisErrorReporter.ResetStats();
        }
    }
}
