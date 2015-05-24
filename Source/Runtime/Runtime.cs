﻿//-----------------------------------------------------------------------
// <copyright file="Runtime.cs" company="Microsoft">
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
using System.Linq;
using System.Threading.Tasks;

using Microsoft.PSharp.BugFinding;
using Microsoft.PSharp.Tooling;

namespace Microsoft.PSharp
{
    /// <summary>
    /// Static class implementing the P# runtime.
    /// </summary>
    public static class Runtime
    {
        #region fields

        /// <summary>
        /// Set of registered state machine types.
        /// </summary>
        private static HashSet<Type> RegisteredMachineTypes = new HashSet<Type>();

        /// <summary>
        /// Set of registered monitor types.
        /// </summary>
        private static HashSet<Type> RegisteredMonitorTypes = new HashSet<Type>();

        /// <summary>
        /// Set of registered event types.
        /// </summary>
        private static HashSet<Type> RegisteredEventTypes = new HashSet<Type>();

        /// <summary>
        /// List of monitors in the program.
        /// </summary>
        private static List<Monitor> Monitors = new List<Monitor>();

        /// <summary>
        /// Lock used by the runtime.
        /// </summary>
        private static Object Lock = new Object();

        /// <summary>
        /// True if runtime is running. False otherwise.
        /// </summary>
        private static bool IsRunning = false;

        /// <summary>
        /// List of machine tasks.
        /// </summary>
        private static List<Task> MachineTasks = new List<Task>();

        /// <summary>
        /// The P# bug-finder.
        /// </summary>
        internal static Scheduler BugFinder = null;

        #endregion

        #region P# API methods

        /// <summary>
        /// Register a new event type. Cannot register a new event
        /// type after the runtime has started running.
        /// </summary>
        /// <param name="e">Event</param>
        public static void RegisterNewEvent(Type e)
        {
            Runtime.Assert(Runtime.IsRunning == false, "Cannot register event '{0}'" +
                "because the P# runtime has already started.", e.Name);
            Runtime.Assert(e.IsSubclassOf(typeof(Event)), "Type '{0}' is not " +
                    "a subclass of Event.", e.Name);
            Runtime.RegisteredEventTypes.Add(e);
        }

        /// <summary>
        /// Register a new machine type. Cannot register a new machine
        /// type after the runtime has started running.
        /// </summary>
        /// <param name="m">Machine</param>
        public static void RegisterNewMachine(Type m)
        {
            Runtime.Assert(Runtime.IsRunning == false, "Cannot register machine '{0}'" +
                "because the P# runtime has already started.", m.Name);
            Runtime.Assert(m.IsSubclassOf(typeof(Machine)), "Type '{0}' is not " +
                    "a subclass of Machine.", m.Name);

            if (m.IsDefined(typeof(Main), false))
            {
                Runtime.Assert(!Runtime.RegisteredMachineTypes.Any(val =>
                    val.IsDefined(typeof(Main), false)),
                    "Machine '{0}' cannot be declared as main. A main machine already " +
                    "exists.", m.Name);
            }

            Runtime.RegisteredMachineTypes.Add(m);
        }

        /// <summary>
        /// Register a new monitor type. Cannot register a new monitor
        /// type after the runtime has started running.
        /// </summary>
        /// <param name="m">Machine</param>
        public static void RegisterNewMonitor(Type m)
        {
            Runtime.Assert(Runtime.IsRunning == false, "Cannot register monitor '{0}'" +
                "because the P# runtime has already started.", m.Name);
            Runtime.Assert(m.IsSubclassOf(typeof(Monitor)), "Type '{0}' is not " +
                    "a subclass of Monitor.", m.Name);
            Runtime.Assert(!m.IsDefined(typeof(Main), false),
                "Monitor '{0}' cannot be declared as main.", m.Name);
            Runtime.RegisteredMonitorTypes.Add(m);
        }

        /// <summary>
        /// Starts the P# runtime by invoking the main machine. The
        /// main machine is constructed with an optional payload.
        /// </summary>
        /// <param name="payload">Optional payload</param>
        public static void Start(params Object[] payload)
        {
            Runtime.Initialize();

            Runtime.Assert(Runtime.RegisteredMachineTypes.Any(val =>
                    val.IsDefined(typeof(Main), false)),
                    "No main machine is registered.");

            // Start the main machine.
            Type mainMachine = Runtime.RegisteredMachineTypes.First(val =>
                val.IsDefined(typeof(Main), false));
            Machine.Factory.Create(mainMachine, payload);

            Runtime.Wait();
        }

        /// <summary>
        /// Waits until the P# runtime has finished.
        /// </summary>
        public static void Wait()
        {
            Task[] taskArray = null;

            while (Runtime.IsRunning)
            {
                lock (Runtime.Lock)
                {
                    taskArray = Runtime.MachineTasks.ToArray();
                }
                
                try
                {
                    Task.WaitAll(taskArray);
                }
                catch (AggregateException)
                {
                    break;
                }

                bool moreTasksExist = false;
                lock (Runtime.Lock)
                {
                    moreTasksExist = taskArray.Length != Runtime.MachineTasks.Count;
                }

                if (!moreTasksExist)
                {
                    break;
                }
            }

            Runtime.Dispose();
        }

#endregion

#region P# runtime internal methods

        /// <summary>
        /// Attempts to create a new machine instance of type T with
        /// the given payload.
        /// </summary>
        /// <param name="m">Type of the machine</param>
        /// <param name="payload">Optional payload</param>
        /// <returns>Machine</returns>
        internal static Machine TryCreateNewMachineInstance(Type m, params Object[] payload)
        {
            Runtime.Assert(Runtime.RegisteredMachineTypes.Any(val => val == m),
                "Machine '{0}' has not been registered with the P# runtime.", m.Name);

            Machine machine = Activator.CreateInstance(m) as Machine;
            machine.AssignInitialPayload(payload);
            Output.Verbose("<CreateLog> Machine {0}({1}) is created.", m, machine.Id);

            Task task = new Task(() =>
            {
                if (Runtime.Options.FindBugs)
                {
                    Runtime.BugFinder.NotifyTaskStarted(Task.CurrentId);
                }
                
                machine.Run();

                if (Runtime.Options.FindBugs)
                {
                    Runtime.BugFinder.NotifyTaskCompleted(Task.CurrentId);
                }
            });

            lock (Runtime.Lock)
            {
                Runtime.MachineTasks.Add(task);
            }

            if (Runtime.Options.FindBugs)
            {
                Runtime.BugFinder.NotifyNewTaskCreated(task.Id, machine);
            }

            task.Start();

            if (Runtime.Options.FindBugs)
            {
                Runtime.BugFinder.WaitForTaskToStart(task.Id);
            }

            return machine;
        }

        /// <summary>
        /// Attempts to create a new machine instance of type T with
        /// the given payload.
        /// </summary>
        /// <typeparam name="T">Type of the machine</typeparam>
        /// <param name="creator">Creator machine</param>
        /// <param name="payload">Optional payload</param>
        /// <returns>Machine</returns>
        internal static T TryCreateNewMachineInstance<T>(Machine creator, params Object[] payload)
        {
            Runtime.Assert(Runtime.RegisteredMachineTypes.Any(val => val == typeof(T)),
                "Machine '{0}' has not been registered with the P# runtime.", typeof(T).Name);

            Object machine = Activator.CreateInstance(typeof(T));
            (machine as Machine).AssignInitialPayload(payload);
            Output.Verbose("<CreateLog> Machine {0}({1}) is created.", typeof(T),
                (machine as Machine).Id);

            Task task = new Task(() =>
            {
                if (Runtime.Options.FindBugs)
                {
                    Runtime.BugFinder.NotifyTaskStarted(Task.CurrentId);
                }
                
                (machine as Machine).Run();

                if (Runtime.Options.FindBugs)
                {
                    Runtime.BugFinder.NotifyTaskCompleted(Task.CurrentId);
                }
            });

            lock (Runtime.Lock)
            {
                Runtime.MachineTasks.Add(task);
            }

            if (Runtime.Options.FindBugs)
            {
                Runtime.BugFinder.NotifyNewTaskCreated(task.Id, machine as Machine);
            }

            task.Start();

            if (Runtime.Options.FindBugs)
            {
                Runtime.BugFinder.WaitForTaskToStart(task.Id);
                Runtime.BugFinder.Schedule(Task.CurrentId);
            }

            return (T)machine;
        }

        /// <summary>
        /// Attempts to create a new monitor instance of type T with
        /// the given payload. There can be only one monitor instance
        /// of each monitor type.
        /// </summary>
        /// <param name="m">Type of the monitor</param>
        /// <param name="payload">Optional payload</param>
        internal static void TryCreateNewMonitorInstance(Type m, params Object[] payload)
        {
            if (!Runtime.Options.FindBugs)
            {
                return;
            }

            Runtime.Assert(Runtime.RegisteredMonitorTypes.Any(val => val == m),
                "Monitor '{0}' has not been registered with the P# runtime.", m.Name);
            Runtime.Assert(!Runtime.Monitors.Any(val => val.GetType() == m),
                "A monitor of type '{0}' already exists.", m.Name);

            Monitor monitor = Activator.CreateInstance(m) as Monitor;
            Output.Verbose("<CreateLog> Monitor {0} is created.", m);

            Runtime.Monitors.Add(monitor);
        }

        /// <summary>
        /// Attempts to create a new monitor instance of type T with
        /// the given payload. There can be only one monitor instance
        /// of each monitor type.
        /// </summary>
        /// <typeparam name="T">Type of the monitor</typeparam>
        /// <param name="payload">Optional payload</param>
        internal static void TryCreateNewMonitorInstance<T>(params Object[] payload)
        {
            if (!Runtime.Options.FindBugs)
            {
                return;
            }

            Runtime.Assert(Runtime.RegisteredMonitorTypes.Any(val => val == typeof(T)),
                "Monitor '{0}' has not been registered with the P# runtime.", typeof(T).Name);
            Runtime.Assert(!Runtime.Monitors.Any(val => val.GetType() == typeof(T)),
                "A monitor of type '{0}' already exists.", typeof(T).Name);

            Object monitor = Activator.CreateInstance(typeof(T));
            Output.Verbose("<CreateLog> Monitor {0} is created.", typeof(T));

            Runtime.Monitors.Add(monitor as Monitor);
        }

        /// <summary>
        /// Attempts to send (i.e. enqueue) an asynchronous event to a machine.
        /// </summary>
        /// <param name="sender">Sender machine</param>
        /// <param name="target">Target machine</param>
        /// <param name="e">Event</param>
        internal static void Send(Machine sender, Machine target, Event e)
        {
            Runtime.Assert(e != null, "Machine '{0}' received a null event.", target);
            Runtime.Assert(Runtime.RegisteredEventTypes.Any(val => val == e.GetType()),
                "Event '{0}' has not been registered with the P# runtime.", e);
            
            target.Enqueue(e);

            if (Runtime.Options.FindBugs &&
                Runtime.BugFinder.HasEnabledTaskForMachine(target))
            {
                Runtime.BugFinder.Schedule(Task.CurrentId);
                return;
            }

            Task task = new Task(() =>
            {
                if (Runtime.Options.FindBugs)
                {
                    Runtime.BugFinder.NotifyTaskStarted(Task.CurrentId);
                }

                target.Run();

                if (Runtime.Options.FindBugs)
                {
                    Runtime.BugFinder.NotifyTaskCompleted(Task.CurrentId);
                }
            });

            lock (Runtime.Lock)
            {
                Runtime.MachineTasks.Add(task);
            }

            if (Runtime.Options.FindBugs)
            {
                Runtime.BugFinder.NotifyNewTaskCreated(task.Id, target);
            }

            task.Start();

            if (Runtime.Options.FindBugs)
            {
                Runtime.BugFinder.WaitForTaskToStart(task.Id);
                Runtime.BugFinder.Schedule(Task.CurrentId);
            }
        }

        /// <summary>
        /// Invokes the specified monitor with the given event.
        /// </summary>
        /// <typeparam name="T">Type of the monitor</typeparam>
        /// <param name="e">Event</param>
        internal static void Monitor<T>(Event e)
        {
            if (!Runtime.Options.FindBugs)
            {
                return;
            }

            Runtime.Assert(Runtime.Monitors.Any(val => val.GetType() == typeof(T)),
                "A monitor of type '{0}' does not exist.", typeof(T).Name);
            Runtime.Assert(Runtime.RegisteredEventTypes.Any(val => val == e.GetType()),
                "Event '{0}' has not been registered with the P# runtime.", e.GetType().Name);

            foreach (var m in Runtime.Monitors)
            {
                if (m.GetType() == typeof(T))
                {
                    m.Enqueue(e, null);
                }
            }
        }

        /// <summary>
        /// Returns all registered event types.
        /// </summary>
        /// <returns>List of event types</returns>
        internal static List<Type> GetRegisteredEventTypes()
        {
            return Runtime.RegisteredEventTypes.ToList();
        }

        /// <summary>
        /// Returns the machine type of the given string.
        /// </summary>
        /// <param name="m">String</param>
        /// <returns>Type of the machine</returns>
        internal static Type GetMachineType(string m)
        {
            Type result = Runtime.RegisteredMachineTypes.FirstOrDefault(t => t.Name.Equals(m));
            Runtime.Assert(result != null, "No machine of type '{0}' was found.", m);
            return result;
        }

#endregion

#region P# runtime private methods

        /// <summary>
        /// Initializes the P# runtime.
        /// </summary>
        private static void Initialize()
        {
            Runtime.RegisterNewEvent(typeof(Halt));
            Runtime.RegisterNewEvent(typeof(Default));

            if (Runtime.Options.FindBugs)
            {
                Runtime.Assert(Runtime.BugFinder != null, "Bugfinder is not initialized.");
            }

            Runtime.IsRunning = true;
        }

        #endregion

        #region runtime options

        /// <summary>
        /// Static class implementing options for the P# runtime.
        /// </summary>
        internal static class Options
        {
            /// <summary>
            /// Run the runtime in bugfinding mode.
            /// </summary>
            internal static bool FindBugs = false;

            /// <summary>
            /// Switch verbose mode on.
            /// </summary>
            internal static bool Verbose = false;
        }

#endregion

#region error checking

        /// <summary>
        /// Checks if the assertion holds, and if not it reports
        /// an error and exits.
        /// </summary>
        /// <param name="predicate">Predicate</param>
        internal static void Assert(bool predicate)
        {
            if (!predicate)
            {
                ErrorReporter.Report("Assertion failure.");

                if (Runtime.Options.FindBugs)
                {
                    Runtime.BugFinder.NotifyAssertionFailure();
                }
                else
                {
                    Environment.Exit(1);
                }
            }
        }

        /// <summary>
        /// Checks if the assertion holds, and if not it reports
        /// an error and exits.
        /// </summary>
        /// <param name="predicate">Predicate</param>
        /// <param name="s">Message</param>
        /// <param name="args">Message arguments</param>
        internal static void Assert(bool predicate, string s, params object[] args)
        {
            if (!predicate)
            {
                string message = Output.Format(s, args);
                ErrorReporter.Report(message);

                if (Runtime.Options.FindBugs)
                {
                    Runtime.BugFinder.NotifyAssertionFailure();
                }
                else
                {
                    Environment.Exit(1);
                }
            }
        }

#endregion

#region cleanup methods

        /// <summary>
        /// Disposes resources of the P# runtime.
        /// </summary>
        internal static void Dispose()
        {
            if (!Runtime.IsRunning)
            {
                return;
            }

            Runtime.RegisteredMachineTypes.Clear();
            Runtime.RegisteredMonitorTypes.Clear();
            Runtime.RegisteredEventTypes.Clear();

            Runtime.Monitors.Clear();
            Runtime.MachineTasks.Clear();

            Machine.ResetMachineIDCounter();

            Runtime.IsRunning = false;
        }

#endregion
    }
}
