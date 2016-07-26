﻿//-----------------------------------------------------------------------
// <copyright file="TestingProcessScheduler.cs">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;

using Microsoft.PSharp.TestingServices.Coverage;
using Microsoft.PSharp.Utilities;

namespace Microsoft.PSharp.TestingServices
{
    /// <summary>
    /// The P# testing process scheduler.
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    internal sealed class TestingProcessScheduler : ITestingProcessScheduler
    {
        #region fields

        /// <summary>
        /// Configuration.
        /// </summary>
        private Configuration Configuration;

        /// <summary>
        /// Map from testing process ids to testing processes.
        /// </summary>
        private Dictionary<int, Process> TestingProcesses;

        /// <summary>
        /// The notification listening service.
        /// </summary>
        private ServiceHost NotificationService;
        
        /// <summary>
        /// The testing coverage info per process.
        /// </summary>
        private ConcurrentDictionary<int, CoverageInfo> CoverageInfos;

        /// <summary>
        /// The testing profiler.
        /// </summary>
        private Profiler Profiler;

        /// <summary>
        /// The scheduler lock.
        /// </summary>
        private object SchedulerLock;

        /// <summary>
        /// The process id of the process that
        /// discovered a bug, else -1.
        /// </summary>
        private int BugFoundByProcess;

        #endregion

        #region remote testing process methods

        /// <summary>
        /// Notifies the testing process scheduler
        /// that a bug was found.
        /// </summary>
        /// <param name="processId">Unique process id</param>
        /// <returns>Boolean value</returns>
        bool ITestingProcessScheduler.NotifyBugFound(int processId)
        {
            bool result = false;
            lock (this.SchedulerLock)
            {
                if (this.BugFoundByProcess < 0)
                {
                    IO.PrintLine($"... Testing task '{processId}' " +
                        "found a bug.");

                    this.BugFoundByProcess = processId;
                    foreach (var testingProcess in this.TestingProcesses)
                    {
                        if (testingProcess.Key == processId)
                        {
                            result = true;
                        }
                        else
                        {
                            if (this.Configuration.ReportCodeCoverage)
                            {
                                var coverageInfo = this.GetCoverageData(testingProcess.Key);
                                this.CoverageInfos.TryAdd(testingProcess.Key, coverageInfo);
                            }

                            testingProcess.Value.Kill();
                        }
                    }
                }
            }
            
            return result;
        }

        /// <summary>
        /// Sends the coverage data.
        /// </summary>
        /// <param name="coverageInfo">CoverageInfo</param>
        /// <param name="processId">Unique process id</param>
        void ITestingProcessScheduler.SetCoverageData(CoverageInfo coverageInfo, int processId)
        {
            this.CoverageInfos.TryAdd(processId, coverageInfo);
        }

        /// <summary>
        /// Gets the global coverage data for the specified process.
        /// </summary>
        /// <param name="processId">Unique process id</param>
        /// <returns>List of CoverageInfo</returns>
        IList<CoverageInfo> ITestingProcessScheduler.GetGlobalCoverageData(int processId)
        {
            var globalCoverageInfo = new List<CoverageInfo>();
            globalCoverageInfo.AddRange(this.CoverageInfos.Where(
                val => val.Key != processId).Select(val => val.Value));
            return globalCoverageInfo;
        }

        /// <summary>
        /// Checks if the specified process should emit coverage data.
        /// </summary>
        /// <param name="processId">Unique process id</param>
        /// <returns>Boolean value</returns>
        bool ITestingProcessScheduler.ShouldEmitCoverageData(int processId)
        {
            lock (this.SchedulerLock)
            {
                if (this.BugFoundByProcess == processId)
                {
                    return true;
                }

                if (this.TestingProcesses.Where(val => val.Key != processId).
                    All(val => val.Value.HasExited))
                {
                    return true;
                }

                return false;
            }
        }

        #endregion

        #region internal methods

        /// <summary>
        /// Creates a new P# testing process scheduler.
        /// </summary>
        /// <param name="configuration">Configuration</param>
        /// <returns>TestingProcessScheduler</returns>
        internal static TestingProcessScheduler Create(Configuration configuration)
        {
            return new TestingProcessScheduler(configuration);
        }

        /// <summary>
        /// Runs the P# testing scheduler.
        /// </summary>
        internal void Run()
        {
            // Opens the remote notification listener.
            // Requires administrator access.
            this.OpenNotificationListener();

            // Creates the user specified number of testing processes.
            for (int testId = 0; testId < this.Configuration.ParallelBugFindingTasks; testId++)
            {
                this.TestingProcesses.Add(testId, TestingProcessFactory.Create(testId, this.Configuration));
            }

            IO.PrintLine($"... Created '{this.Configuration.ParallelBugFindingTasks}' " +
                "parallel testing tasks.");

            this.Profiler.StartMeasuringExecutionTime();

            // Starts the testing processes.
            for (int testId = 0; testId < this.Configuration.ParallelBugFindingTasks; testId++)
            {
                this.TestingProcesses[testId].Start();
            }

            // Waits the testing processes to exit.
            for (int testId = 0; testId < this.Configuration.ParallelBugFindingTasks; testId++)
            {
                this.TestingProcesses[testId].WaitForExit();
            }

            this.Profiler.StopMeasuringExecutionTime();

            IO.PrintLine($"... Parallel testing elapsed {this.Profiler.Results()} sec.");
        }

        #endregion

        #region constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">Configuration</param>
        private TestingProcessScheduler(Configuration configuration)
        {
            this.TestingProcesses = new Dictionary<int, Process>();
            this.CoverageInfos = new ConcurrentDictionary<int, CoverageInfo>();
            this.Profiler = new Profiler();
            this.SchedulerLock = new object();
            this.BugFoundByProcess = -1;

            configuration.Verbose = 1;
            configuration.PrintTrace = false;
            configuration.PerformFullExploration = false;
            configuration.EnableDataRaceDetection = false;

            this.Configuration = configuration;
        }

        #endregion

        #region private methods

        /// <summary>
        /// Opens the remote notification listener.
        /// </summary>
        private void OpenNotificationListener()
        {
            Uri address = new Uri("http://localhost:8080/psharp/testing/scheduler/");

            WSHttpBinding binding = new WSHttpBinding();
            binding.MaxReceivedMessageSize = Int32.MaxValue;

            this.NotificationService = new ServiceHost(this);
            this.NotificationService.AddServiceEndpoint(typeof(ITestingProcessScheduler), binding, address);

            if (this.Configuration.EnableDebugging)
            {
                ServiceDebugBehavior debug = this.NotificationService.Description.
                    Behaviors.Find<ServiceDebugBehavior>();
                debug.IncludeExceptionDetailInFaults = true;
            }
            
            try
            {
                this.NotificationService.Open();
            }
            catch (AddressAccessDeniedException)
            {
                IO.Error.ReportAndExit("Your process does not have access " +
                    "rights to open the remote testing notification listener. " +
                    "Please run the process as administrator.");
            }
        }

        /// <summary>
        /// Gets the coverage data associated with the specified testing process.
        /// </summary>
        /// <param name="processId">Unique process id</param>
        /// <returns>CoverageInfo</returns>
        private CoverageInfo GetCoverageData(int processId)
        {
            Uri address = new Uri("http://localhost:8080/psharp/testing/process/" + processId + "/");

            WSHttpBinding binding = new WSHttpBinding();
            binding.MaxReceivedMessageSize = Int32.MaxValue;

            EndpointAddress endpoint = new EndpointAddress(address);

            var testingProcess = ChannelFactory<ITestingProcess>.
                    CreateChannel(binding, endpoint);

            return testingProcess.GetCoverageData();
        }

        #endregion
    }
}
