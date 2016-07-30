﻿//-----------------------------------------------------------------------
// <copyright file="TestReport.cs">
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

using System.Runtime.Serialization;
using System.Text;

using Microsoft.PSharp.TestingServices.Coverage;
using Microsoft.PSharp.Utilities;

namespace Microsoft.PSharp.TestingServices
{
    /// <summary>
    /// Class implementing the P# test report.
    /// </summary>
    [DataContract]
    public class TestReport
    {
        #region properties

        /// <summary>
        /// Information regarding code coverage.
        /// </summary>
        [DataMember]
        public CoverageInfo CoverageInfo { get; private set; }

        /// <summary>
        /// Number of explored fair schedules.
        /// </summary>
        [DataMember]
        public int NumOfExploredFairSchedules { get; internal set; }

        /// <summary>
        /// Number of explored unfair schedules.
        /// </summary>
        [DataMember]
        public int NumOfExploredUnfairSchedules { get; internal set; }

        /// <summary>
        /// Number of found bugs.
        /// </summary>
        [DataMember]
        public int NumOfFoundBugs { get; internal set; }

        /// <summary>
        /// The latest bug report, if any.
        /// </summary>
        [DataMember]
        public string BugReport { get; internal set; }

        /// <summary>
        /// The total explored scheduling steps (across
        /// all testing iterations), in fair tests.
        /// </summary>
        [DataMember]
        public int TotalExploredFairSteps { get; internal set; }

        /// <summary>
        /// The min explored scheduling steps in average,
        /// in fair tests.
        /// </summary>
        [DataMember]
        public int MinExploredFairSteps { get; internal set; }

        /// <summary>
        /// The max explored scheduling steps in average,
        /// in fair tests.
        /// </summary>
        [DataMember]
        public int MaxExploredFairSteps { get; internal set; }

        /// <summary>
        /// Number of times the max steps bound was hit.
        /// </summary>
        [DataMember]
        public int MaxStepsHit { get; internal set; }

        #endregion

        #region constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        public TestReport()
        {
            this.CoverageInfo = new CoverageInfo();

            this.NumOfExploredFairSchedules = 0;
            this.NumOfExploredUnfairSchedules = 0;
            this.NumOfFoundBugs = 0;
            this.BugReport = "";
            this.TotalExploredFairSteps = 0;
            this.MinExploredFairSteps = -1;
            this.MaxExploredFairSteps = -1;
            this.MaxStepsHit = 0;
        }

        #endregion

        #region public methods

        /// <summary>
        /// Merges the information from the specified
        /// test report. This is not thread-safe.
        /// </summary>
        /// <param name="testReport">TestReport</param>
        public void Merge(TestReport testReport)
        {
            this.CoverageInfo.Merge(testReport.CoverageInfo);

            this.NumOfExploredFairSchedules += testReport.NumOfExploredFairSchedules;
            this.NumOfExploredUnfairSchedules += testReport.NumOfExploredUnfairSchedules;
            this.MaxStepsHit += testReport.MaxStepsHit;

            if (testReport.MinExploredFairSteps >= 0 &&
                this.MinExploredFairSteps > testReport.MinExploredFairSteps)
            {
                this.MinExploredFairSteps = testReport.MinExploredFairSteps;
            }

            if (this.MaxExploredFairSteps < testReport.MaxExploredFairSteps)
            {
                this.MaxExploredFairSteps = testReport.MaxExploredFairSteps;
            }

            this.TotalExploredFairSteps += testReport.TotalExploredFairSteps;
        }

        #endregion
    }
}