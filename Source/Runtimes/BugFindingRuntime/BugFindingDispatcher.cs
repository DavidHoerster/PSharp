﻿//-----------------------------------------------------------------------
// <copyright file="BugFindingDispatcher.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
//      EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
//      OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
// ----------------------------------------------------------------------------------
//      The example companies, organizations, products, domain names,
//      e-mail addresses, logos, people, places, and events depicted
//      herein are fictitious.  No association with any real company,
//      organization, product, domain name, email address, logo, person,
//      places, or events is intended or should be inferred.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.PSharp.Utilities;

namespace Microsoft.PSharp
{
    /// <summary>
    /// Class implementing the dispatcher that handles the
    /// communication with the P# runtime.
    /// </summary>
    internal sealed class BugFindingDispatcher : IDispatcher
    {
        #region API methods

        /// <summary>
        /// Tries to create a new machine of the given type.
        /// </summary>
        /// <param name="type">Type of the machine</param>
        /// <returns>MachineId</returns>
        MachineId IDispatcher.TryCreateMachine(Type type)
        {
            return PSharpRuntime.TryCreateMachine(type);
        }

        /// <summary>
        /// Tries to create a new remote machine of the given type.
        /// </summary>
        /// <param name="type">Type of the machine</param>
        /// <returns>MachineId</returns>
        MachineId IDispatcher.TryCreateRemoteMachine(Type type)
        {
            // Remote does not work in the bug-finding runtime.
            return PSharpRuntime.TryCreateMachine(type);
        }

        /// <summary>
        /// Tries to create a new monitor of the given type.
        /// </summary>
        /// <param name="type">Type of the machine</param>
        void IDispatcher.TryCreateMonitor(Type type)
        {
            PSharpRuntime.TryCreateMonitor(type);
        }

        /// <summary>
        /// Tries to create a new task machine.
        /// </summary>
        /// <param name="userTask">Task</param>
        void IDispatcher.TryCreateTaskMachine(Task userTask)
        {
            PSharpRuntime.TryCreateTaskMachine(userTask);
        }

        /// <summary>
        /// Sends an asynchronous event to a machine.
        /// </summary>
        /// <param name="mid">MachineId</param>
        /// <param name="e">Event</param>
        void IDispatcher.Send(MachineId mid, Event e)
        {
            PSharpRuntime.Send(mid, e);
        }

        /// <summary>
        /// Invokes the specified monitor with the given event.
        /// </summary>
        /// <typeparam name="T">Type of the monitor</typeparam>
        /// <param name="e">Event</param>
        void IDispatcher.Monitor<T>(Event e)
        {
            PSharpRuntime.Monitor<T>(e);
        }

        /// <summary>
        /// Returns a nondeterministic boolean choice, that can be
        /// controlled during analysis or testing.
        /// </summary>
        /// <returns>Boolean</returns>
        bool IDispatcher.Random()
        {
            return PSharpRuntime.GetNondeterministicChoice();
        }

        /// <summary>
        /// Returns a fair nondeterministic boolean choice, that can be
        /// controlled during analysis or testing.
        /// </summary>
        /// <returns>Boolean</returns>
        bool IDispatcher.FairRandom()
        {
            return PSharpRuntime.GetNondeterministicChoice();
        }

        /// <summary>
        /// Returns a fair nondeterministic boolean choice, that can be
        /// controlled during analysis or testing.
        /// </summary>
        /// <param name="uniqueId">Unique id</param>
        /// <returns>Boolean</returns>
        bool IDispatcher.FairRandom(string uniqueId)
        {
            return PSharpRuntime.GetFairNondeterministicChoice(uniqueId);
        }

        /// <summary>
        /// Checks if the assertion holds, and if not it reports
        /// an error and exits.
        /// </summary>
        /// <param name="predicate">Predicate</param>
        void IDispatcher.Assert(bool predicate)
        {
            PSharpRuntime.Assert(predicate);
        }

        /// <summary>
        /// Checks if the assertion holds, and if not it reports
        /// an error and exits.
        /// </summary>
        /// <param name="predicate">Predicate</param>
        /// <param name="s">Message</param>
        /// <param name="args">Message arguments</param>
        void IDispatcher.Assert(bool predicate, string s, params object[] args)
        {
            PSharpRuntime.Assert(predicate, s, args);
        }

        /// <summary>
        /// Logs the given text with the runtime.
        /// </summary>
        /// <param name="s">String</param>
        /// <param name="args">Arguments</param>
        void IDispatcher.Log(string s, params object[] args)
        {
            Output.Log(s, args);
        }

        /// <summary>
        /// Notifies that a machine is waiting to receive an event.
        /// </summary>
        /// <param name="mid">MachineId</param>
        void IDispatcher.NotifyWaitEvent(MachineId mid)
        {
            PSharpRuntime.NotifyWaitEvent();
        }

        /// <summary>
        /// Notifies that a machine received an event that it was waiting for.
        /// </summary>
        /// <param name="mid">MachineId</param>
        void IDispatcher.NotifyReceivedEvent(MachineId mid)
        {
            PSharpRuntime.NotifyReceivedEvent(mid);
        }

        /// <summary>
        /// Notifies that a default handler has been used.
        /// </summary>
        void IDispatcher.NotifyDefaultHandlerFired()
        {
            PSharpRuntime.NotifyDefaultHandlerFired();
        }

        /// <summary>
        /// Notifies that a scheduling point should be instrumented
        /// due to a wait synchronization operation.
        /// </summary>
        /// <param name="blockingTasks">Blocking tasks</param>
        /// <param name="waitAll">Boolean value</param>
        void IDispatcher.ScheduleOnWait(IEnumerable<Task> blockingTasks, bool waitAll)
        {
            PSharpRuntime.ScheduleOnWait(blockingTasks, waitAll);
        }

        #endregion
    }
}
