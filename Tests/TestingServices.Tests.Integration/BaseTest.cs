﻿//-----------------------------------------------------------------------
// <copyright file="BaseTest.cs">
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
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.PSharp.IO;
using Microsoft.PSharp.Utilities;

using Xunit;

namespace Microsoft.PSharp.TestingServices.Tests.Integration
{
    public abstract class BaseTest
    {
        #region successful tests

        protected void AssertSucceeded(Action<PSharpRuntime> test)
        {
            var configuration = GetConfiguration();
            AssertSucceeded(configuration, test);
        }

        protected void AssertSucceeded(Configuration configuration, Action<PSharpRuntime> test)
        {
            InMemoryLogger logger = new InMemoryLogger();

            try
            {
                BugFindingEngine engine = BugFindingEngine.Create(configuration, test);
                engine.SetLogger(logger);
                engine.Run();

                var numErrors = engine.TestReport.NumOfFoundBugs;
                Assert.True(numErrors == 0, GetBugReport(engine));
            }
            catch (Exception ex)
            {
                Assert.False(true, ex.Message + "\n" + ex.StackTrace);
            }
            finally
            {
                logger.Dispose();
            }
        }

        #endregion

        #region failed tests

        protected void AssertFailed(Action<PSharpRuntime> test, int numExpectedErrors)
        {
            var configuration = GetConfiguration();
            AssertFailed(configuration, test, numExpectedErrors);
        }

        protected void AssertFailed(Action<PSharpRuntime> test, string expectedOutput)
        {
            var configuration = GetConfiguration();
            AssertFailed(configuration, test, 1, new HashSet<string> { expectedOutput });
        }

        protected void AssertFailed(Action<PSharpRuntime> test, int numExpectedErrors, ISet<string> expectedOutputs)
        {
            var configuration = GetConfiguration();
            AssertFailed(configuration, test, numExpectedErrors, expectedOutputs);
        }

        protected void AssertFailed(Configuration configuration, Action<PSharpRuntime> test, int numExpectedErrors)
        {
            AssertFailed(configuration, test, numExpectedErrors, new HashSet<string>());
        }

        protected void AssertFailed(Configuration configuration, Action<PSharpRuntime> test, string expectedOutput)
        {
            AssertFailed(configuration, test, 1, new HashSet<string> { expectedOutput });
        }

        protected void AssertFailed(Configuration configuration, Action<PSharpRuntime> test, int numExpectedErrors, ISet<string> expectedOutputs)
        {
            InMemoryLogger logger = new InMemoryLogger();

            try
            {
                var bfEngine = BugFindingEngine.Create(configuration, test);
                bfEngine.SetLogger(logger);
                bfEngine.Run();

                CheckErrors(bfEngine, numExpectedErrors, expectedOutputs);

                if (!configuration.EnableCycleReplayingStrategy && !configuration.CacheProgramState)
                {
                    var rEngine = ReplayEngine.Create(configuration, test, bfEngine.ReproducableTrace);
                    rEngine.SetLogger(logger);
                    rEngine.Run();

                    Assert.True(rEngine.InternalError.Length == 0, rEngine.InternalError);
                    CheckErrors(rEngine, numExpectedErrors, expectedOutputs);
                }
            }
            catch (Exception ex)
            {
                Assert.False(true, ex.Message + "\n" + ex.StackTrace);
            }
            finally
            {
                logger.Dispose();
            }
        }

        private void CheckErrors(ITestingEngine engine, int numExpectedErrors, ISet<string> expectedOutputs)
        {
            var numErrors = engine.TestReport.NumOfFoundBugs;
            Assert.Equal(numExpectedErrors, numErrors);

            if (expectedOutputs.Count > 0)
            {
                var bugReports = new HashSet<string>();
                foreach (var bugReport in engine.TestReport.BugReports)
                {
                    var actual = this.RemoveNonDeterministicValuesFromReport(bugReport);
                    bugReports.Add(actual);
                }

                foreach (var expected in expectedOutputs)
                {
                    Assert.Contains(expected, bugReports);
                }
            }
        }

        #endregion

        #region utilities

        protected Configuration GetConfiguration()
        {
            return Configuration.Create();
        }

        private string GetBugReport(ITestingEngine engine)
        {
            string report = "";
            foreach (var bug in engine.TestReport.BugReports)
            {
                report += bug + "\n";
            }

            return report;
        }

        private string RemoveNonDeterministicValuesFromReport(string report)
        {
            var result = Regex.Replace(report, @"\'[0-9]+\'", "''");
            result = Regex.Replace(result, @"\([0-9]+\)", "()");
            return result;
        }

        #endregion
    }
}
