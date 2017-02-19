﻿//-----------------------------------------------------------------------
// <copyright file="SEMOneMachine35Test.cs">
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
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.PSharp.TestingServices.Tests.Unit
{
    [TestClass]
    public class SEMOneMachine35Test
    {
        class Entry : Machine
        {
            List<int> rev;
            List<int> sorted;
            int i;
            int t;
            int s;
            bool swapped;
            bool b;

            [Start]
            [OnEntry(nameof(EntryInit))]
            class Init : MachineState { }

            void EntryInit()
            {
                rev = new List<int>();
                sorted = new List<int>();

                i = 0;
                while (i < 10)
                {
                    rev.Insert(0, i);
                    sorted.Add(i);
                    i = i + 1;
                }

                this.Assert(rev.Count == 10);

                // Assert that simply reversing the list produces a sorted list
                sorted = Reverse(rev);
                this.Assert(sorted.Count == 10);
                b = IsSorted(sorted);
                this.Assert(b);
                b = IsSorted(rev);
                this.Assert(!b);

                // Assert that BubbleSort returns the sorted list 
                sorted = BubbleSort(rev);
                this.Assert(sorted.Count == 10);
                b = IsSorted(sorted);
                this.Assert(b);
                b = IsSorted(rev);
                this.Assert(!b);
            }

            List<int> Reverse(List<int> l)
            {
                var result = l.ToList();

                i = 0;
                s = result.Count;
                while (i < s)
                {
                    t = result[i];
                    result.RemoveAt(i);
                    result.Insert(0, t);
                    i = i + 1;
                }

                return result;
            }

            List<int> BubbleSort(List<int> l)
            {
                var result = l.ToList();

                swapped = true;
                while (swapped)
                {
                    i = 0;
                    swapped = false;
                    while (i < result.Count - 1)
                    {
                        if (result[i] > result[i + 1])
                        {
                            t = result[i];
                            result[i] = result[i + 1];
                            result[i + 1] = t;
                            swapped = true;
                        }

                        i = i + 1;
                    }
                }

                return result;
            }

            bool IsSorted(List<int> l)
            {
                i = 0;
                while (i < l.Count - 1)
                {
                    if (l[i] > l[i + 1])
                    {
                        return false;
                    }

                    i = i + 1;
                }

                return true;
            }
        }

        public static class TestProgram
        {
            [Test]
            public static void Execute(Runtime runtime)
            {
                runtime.CreateMachine(typeof(Entry));
            }
        }

        [TestMethod]
        public void TestSEMOneMachine35()
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
