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

using Xunit;

namespace Microsoft.PSharp.TestingServices.Tests.Unit
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
                var engine = BugFindingEngine.Create(configuration, test);
                engine.SetLogger(logger);
                engine.Run();

                var numErrors = engine.TestReport.NumOfFoundBugs;
                Assert.Equal(0, numErrors);
            }
            catch (Exception ex)
            {
                Assert.False(true, ex.Message);
            }
            finally
            {
                logger.Dispose();
            }
        }

        #endregion

        #region tests that fail an assertion

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
                var engine = BugFindingEngine.Create(configuration, test);
                engine.SetLogger(logger);
                engine.Run();

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
            catch (Exception ex)
            {
                Assert.False(true, ex.Message);
            }
            finally
            {
                logger.Dispose();
            }
        }

        #endregion

        #region tests that throw an exception

        protected void AssertFailedWithException(Action<PSharpRuntime> test, Type exceptionType)
        {
            var configuration = GetConfiguration();
            AssertFailedWithException(configuration, test, exceptionType);
        }

        protected void AssertFailedWithException(Configuration configuration, Action<PSharpRuntime> test, Type exceptionType)
        {
            Assert.True(exceptionType.IsSubclassOf(typeof(Exception)), "Please configure the test correctly. " +
                $"Type '{exceptionType}' is not an exception type.");

            InMemoryLogger logger = new InMemoryLogger();

            try
            {
                var engine = BugFindingEngine.Create(configuration, test);
                engine.SetLogger(logger);
                engine.Run();

                var numErrors = engine.TestReport.NumOfFoundBugs;
                Assert.Equal(1, numErrors);

                var exception = this.RemoveNonDeterministicValuesFromReport(engine.TestReport.BugReports.First()).
                    Split(new[] { '\r', '\n' }).FirstOrDefault();
                Assert.Contains("'" + exceptionType.ToString() + "'", exception);
            }
            catch (Exception ex)
            {
                Assert.False(true, ex.Message);
            }
            finally
            {
                logger.Dispose();
            }
        }

        #endregion

        #region utilities

        protected Configuration GetConfiguration()
        {
            var configuration = Configuration.Create();
            configuration.SuppressTrace = true;
            return configuration;
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
