﻿//-----------------------------------------------------------------------
// <copyright file="RaceDetectionProcess.cs">
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
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;

using Microsoft.ExtendedReflection.Monitoring;
using Microsoft.ExtendedReflection.Utilities.Safe;

using Microsoft.PSharp.Utilities;

namespace Microsoft.PSharp
{
    /// <summary>
    /// A P# dynamic race detection process.
    /// </summary>
    internal sealed class RaceDetectionProcess
    {
        #region fields

        /// <summary>
        /// Configuration.
        /// </summary>
        private Configuration Configuration;

        /// <summary>
        /// The runtime profiler.
        /// </summary>
        private Profiler Profiler;

        #endregion

        #region API

        /// <summary>
        /// Creates a P# dynamic race detection process.
        /// </summary>
        /// <param name="configuration">Configuration</param>
        /// <returns>RaceDetectionProcess</returns>
        public static RaceDetectionProcess Create(Configuration configuration)
        {
            return new RaceDetectionProcess(configuration);
        }

        /// <summary>
        /// Starts the P# testing process.
        /// </summary>
        public void Start()
        {
            IO.PrintLine(". Testing " + this.Configuration.AssemblyToBeAnalyzed);
            this.MonitorAssembly(this.Configuration.AssemblyToBeAnalyzed);
        }

        #endregion

        #region private methods

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">Configuration</param>
        private RaceDetectionProcess(Configuration configuration)
        {
            this.Profiler = new Profiler();
            this.Configuration = configuration;
        }
        
        /// <summary>
        /// Monitor the given P# assembly.
        /// </summary>
        /// <param name="dll">Assembly</param>
        private void MonitorAssembly(string dll)
        {
            StringCollection referencedAssemblies = new StringCollection();
            string input = this.Configuration.AssemblyToBeAnalyzed;

            Assembly assembly = Assembly.LoadFrom(input);
            referencedAssemblies.Add(assembly.GetName().Name);

            AssemblyName[] assemblyName = assembly.GetReferencedAssemblies();
            foreach (AssemblyName item in assemblyName)
            {
                if (item.Name.Contains("mscorlib") || item.Name.Contains("System") ||
                    item.Name.Contains("NLog") || item.Name.Contains("System.Core"))
                {
                    continue;
                }

                referencedAssemblies.Add(item.Name);
            }

            string[] includedAssemblies = new string[referencedAssemblies.Count];
            referencedAssemblies.CopyTo(includedAssemblies, 0);

            //var newArgs = args.ToList();
            //newArgs.Remove("/race-detection");
            //newArgs.Add("/race-detection-no-monitorable-process");
            
            //this.Configuration.DirectoryPath = ".\\";
            //IEnumerable<string> dirNames = Directory.EnumerateDirectories(this.Configuration.DirectoryPath);
            //foreach (string item in dirNames)
            //{
            //    if (item.Contains("InstrTrace"))
            //    {
            //        Directory.Delete(this.Configuration.DirectoryPath + item, true);
            //    }
            //}

            // Starts profiling the access monitor.
            if (this.Configuration.EnableProfiling)
            {
                this.Profiler.StartMeasuringExecutionTime();
            }

            Profiler.StartMeasuringExecutionTime();

            ProcessStartInfo info = ControllerSetUp.GetMonitorableProcessStartInfo(
                AppDomain.CurrentDomain.BaseDirectory + "\\PSharpThreadMonitor.exe",
                new String[] { WrapString(input) }, // arguments
                MonitorInstrumentationFlags.All, // monitor flags
                true, // track gc accesses

                null, // we don't monitor process at startup since it loads the DLL to monitor
                null, // user type

                null, // substitution assemblies
                null, // types to monitor
                null, // types to exclude monitor
                null, // namespaces to monitor
                null, // namespaces to exclude monitor
                includedAssemblies,
                null, //assembliesToExcludeMonitor to exclude monitor

                null,
                null, null, null,
                null, null,

                null, // clrmonitor log file name
                false, // clrmonitor  log verbose
                null, // crash on failure
                true, // protect all cctors
                false, // disable mscrolib suppressions
                ProfilerInteraction.Fail, // profiler interaction
                null, "", ""
                );

            IO.PrintLine(". Starts monitorable testing process");

            var process = new Process();
            process.StartInfo = info;
            process.Start();
            process.WaitForExit();

            // Stops profiling the access monitor.
            if (this.Configuration.EnableProfiling)
            {
                this.Profiler.StopMeasuringExecutionTime();
                IO.PrintLine("... Access monitoring runtime: '" +
                    this.Profiler.Results() + "' seconds.");
            }

            // Starts profiling the race detection.
            if (this.Configuration.EnableProfiling)
            {
                this.Profiler.StartMeasuringExecutionTime();
            }

            //OfflineRaces.Program.findRaces();

            // Stops profiling the race detection.
            if (this.Configuration.EnableProfiling)
            {
                this.Profiler.StopMeasuringExecutionTime();
                IO.PrintLine("... Race detection runtime: '" +
                    this.Profiler.Results() + "' seconds.");
            }
        }

        /// <summary>
        /// Wraps the given string.
        /// </summary>
        /// <param name="text">Text</param>
        /// <returns></returns>
        private string WrapString(string text)
        {
            if (text == null)
            {
                return text;
            }
            else
            {
                return SafeString.IndexOf(text, ' ') != -1 ? "\"" + text.TrimEnd('\\') + "\"" : text;
            }
        }

        #endregion
    }
}
