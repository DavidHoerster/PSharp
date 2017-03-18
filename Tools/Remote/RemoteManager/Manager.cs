﻿//-----------------------------------------------------------------------
// <copyright file="Manager.cs">
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;

using Microsoft.PSharp.IO;
using Microsoft.PSharp.Utilities;

namespace Microsoft.PSharp.Remote
{
    /// <summary>
    /// Implements a remote manager.
    /// </summary>
    internal static class Manager
    {
        #region fields

        /// <summary>
        /// Configuration.
        /// </summary>
        private static Configuration Configuration;

        /// <summary>
        /// The notification listening service.
        /// </summary>
        private static ServiceHost NotificationService;

        /// <summary>
        /// Map from ids to containers.
        /// </summary>
        private static Dictionary<int, Process> Containers =
            new Dictionary<int, Process>();

        /// <summary>
        /// Map from ids to container services.
        /// </summary>
        private static Dictionary<int, IContainerService> ContainerServices =
            new Dictionary<int, IContainerService>();

        /// <summary>
        /// Monotonically increasing container id counter.
        /// </summary>
        private static int ContainerIdCounter = 0;

        #endregion

        #region internal API

        /// <summary>
        /// Configures the remote manager.
        /// </summary>
        /// <param name="configuration">Configuration</param>
        internal static void Configure(Configuration configuration)
        {
            Manager.Configuration = configuration;
        }

        /// <summary>
        /// Runs the remote manager.
        /// </summary>
        internal static void Run()
        {
            Output.WriteLine(". Running");

            Manager.OpenRemoteManagingListener();

            for (int idx = 0; idx < Manager.Configuration.NumberOfContainers; idx++)
            {
                Manager.CreateContainer();
            }

            Console.ReadLine();

            Output.WriteLine(". Cleaning resources");

            Manager.KillContainers();
            Manager.NotificationService.Close();

            Output.WriteLine("... Closed listener");
        }

        /// <summary>
        /// Notifies that a container has been initialized.
        /// </summary>
        /// <param name="id">Container id</param>
        internal static void NotifyInitializedContainer(int id)
        {
            Output.WriteLine("..... Container '{0}' is initialized", id);

            Uri address = new Uri("http://localhost:8000/notify/" + id + "/");

            var binding = new WSHttpBinding();
            var endpoint = new EndpointAddress(address);

            var channel = ChannelFactory<IContainerService>.CreateChannel(binding, endpoint);
            
            Manager.ContainerServices.Add(id, channel);
            
            if (Manager.ContainerServices.Count == Manager.Configuration.NumberOfContainers)
            {
                Output.WriteLine("... Notifying container '0' [start]");
                Manager.ContainerServices[0].NotifyStartPSharpRuntime();
            }
        }

        #endregion

        #region private methods

        /// <summary>
        /// Creates a new container.
        /// </summary>
        /// <returns>Process</returns>
        private static void CreateContainer()
        {
            Output.WriteLine("... Creating container '{0}'", Manager.ContainerIdCounter);

            Process process = new Process();
            process.StartInfo.FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "PSharpRuntimeContainer.exe");
            process.StartInfo.Arguments = "/id:" + Manager.ContainerIdCounter;
            process.StartInfo.Arguments += " /load:" + Manager.Configuration.RemoteApplicationFilePath;
            process.StartInfo.Arguments += " /v:" + Manager.Configuration.Verbose;

            Manager.Containers.Add(Manager.ContainerIdCounter, process);

            Manager.ContainerIdCounter++;

            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Error.ReportAndExit(ex.Message);
            }
        }

        /// <summary>
        /// Notifies the containers to terminate.
        /// </summary>
        private static void KillContainers()
        {
            Output.WriteLine("... Shutting down containers");
            foreach (var container in Manager.ContainerServices)
            {
                container.Value.NotifyTerminate();
            }
        }

        /// <summary>
        /// Opens the remote managing listener.
        /// </summary>
        private static void OpenRemoteManagingListener()
        {
            Output.WriteLine("... Opening notification listener");

            Uri address = new Uri("http://localhost:8000/manager/");
            var binding = new WSHttpBinding();

            Manager.NotificationService = new ServiceHost(typeof(NotificationListener));

            //host.Description.Behaviors.Remove(typeof(ServiceDebugBehavior));
            //host.Description.Behaviors.Add(new ServiceDebugBehavior {
            //    IncludeExceptionDetailInFaults = true });

            Manager.NotificationService.AddServiceEndpoint(typeof(IManagerService), binding, address);
            Manager.NotificationService.Open();
        }

        #endregion
    }
}
