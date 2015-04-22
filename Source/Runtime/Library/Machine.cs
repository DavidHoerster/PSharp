﻿//-----------------------------------------------------------------------
// <copyright file="Machine.cs" company="Microsoft">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.PSharp.IO;
using Microsoft.PSharp.Scheduling;

namespace Microsoft.PSharp
{
    /// <summary>
    /// Abstract class representing a state machine.
    /// </summary>
    public abstract class Machine
    {
        #region static fields

        /// <summary>
        /// Monotonipushy increasing machine ID counter.
        /// </summary>
        private static int IdCounter = 0;

        #endregion

        #region fields

        /// <summary>
        /// Unique machine ID.
        /// </summary>
        internal readonly int Id;

        /// <summary>
        /// Lock used by the machine.
        /// </summary>
        private Object Lock;

        /// <summary>
        /// Set of all possible states types.
        /// </summary>
        private HashSet<Type> StateTypes;

        /// <summary>
        /// A stack of machine states. The state on the top of
        /// the stack represents the current state.
        /// </summary>
        private Stack<State> StateStack;

        /// <summary>
        /// True if machine has halted.
        /// </summary>
        private bool IsHalted;

        /// <summary>
        /// False if machine has stopped.
        /// </summary>
        private bool IsActive;

        /// <summary>
        /// Cancellation token source for the machine.
        /// </summary>
        private CancellationTokenSource CTS;

        /// <summary>
        /// Collection of all possible goto state transitions.
        /// </summary>
        private Dictionary<Type, GotoStateTransitions> GotoTransitions;

        /// <summary>
        /// Collection of all possible push state transitions.
        /// </summary>
        private Dictionary<Type, PushStateTransitions> PushTransitions;

        /// <summary>
        /// Collection of all possible action bindings.
        /// </summary>
        private Dictionary<Type, ActionBindings> ActionBindings;

        /// <summary>
        /// Inbox of the state machine. Incoming events are
        /// queued here. Events are dequeued to be processed.
        /// </summary>
        private List<Event> Inbox;

        /// <summary>
        /// Inbox of the state machine. Incoming events are
        /// queued here. Events are dequeued to be processed.
        /// A thread-safe blocking collection is used.
        /// </summary>
        //internal BlockingCollection<Event> Inbox;

        /// <summary>
        /// Inbox of the state machine (used during bug-finding mode).
        /// Incoming events are queued here. Events are dequeued to be
        /// processed. A thread-safe blocking collection is used.
        /// </summary>
        private SystematicBlockingQueue<Event> ScheduledInbox;

        /// <summary>
        /// Handle to the latest received event type.
        /// If there was no event received yet the returned
        /// value is null.
        /// </summary>
        protected internal Type Message;

        /// <summary>
        /// Handle to the payload of the last received event.
        /// If the last received event does not have a payload,
        /// a null value is returned.
        /// </summary>
        protected internal Object Payload;

        #endregion

        #region machine constructors

        /// <summary>
        /// Constructor of the Machine class.
        /// </summary>
        protected Machine()
        {
            this.Id = Machine.IdCounter++;
            this.Lock = new Object();

            if (Runtime.Options.Mode == Runtime.Mode.Execution)
                this.Inbox = new List<Event>();
                //this.Inbox = new BlockingCollection<Event>();
            else if (Runtime.Options.Mode == Runtime.Mode.BugFinding)
                this.ScheduledInbox = new SystematicBlockingQueue<Event>();

            this.StateStack = new Stack<State>();
            this.IsHalted = false;
            this.IsActive = true;

            this.CTS = new CancellationTokenSource();

            this.GotoTransitions = this.DefineGotoStateTransitions();
            this.PushTransitions = this.DefinePushStateTransitions();
            this.ActionBindings = this.DefineActionBindings();

            this.InitializeStateInformation();
            this.AssertStateValidity();
        }

        #endregion

        #region P# API methods

        /// <summary>
        /// Defines all possible goto state transitions for each state.
        /// It must return a dictionary where a key represents
        /// a state and a value represents the state's transitions.
        /// </summary>
        /// <returns>Dictionary<Type, StateTransitions></returns>
        protected virtual Dictionary<Type, GotoStateTransitions> DefineGotoStateTransitions()
        {
            return new Dictionary<Type, GotoStateTransitions>();
        }

        /// <summary>
        /// Defines all possible push state transitions for each state.
        /// It must return a dictionary where a key represents
        /// a state and a value represents the state's transitions.
        /// </summary>
        /// <returns>Dictionary<Type, StateTransitions></returns>
        protected virtual Dictionary<Type, PushStateTransitions> DefinePushStateTransitions()
        {
            return new Dictionary<Type, PushStateTransitions>();
        }

        /// <summary>
        /// Defines all possible action bindings for each state.
        /// It must return a dictionary where a key represents
        /// a state and a value represents the state's action bindings.
        /// </summary>
        /// <returns>Dictionary<Type, ActionBindings></returns>
        protected virtual Dictionary<Type, ActionBindings> DefineActionBindings()
        {
            return new Dictionary<Type, ActionBindings>();
        }

        /// <summary>
        /// Sends an asynchronous event to a machine.
        /// </summary>
        /// <param name="m">Machine</param>
        /// <param name="e">Event</param>
        protected internal void Send(Machine m, Event e)
        {
            Runtime.Send(m, e, this.GetType().Name);
        }

        /// <summary>
        /// Invokes the specified monitor with the given event.
        /// </summary>
        /// <typeparam name="T">Type of the monitor</typeparam>
        /// <param name="e">Event</param>
        protected internal void Invoke<T>(Event e)
        {
            Runtime.Invoke<T>(e);
        }

        /// <summary>
        /// Raises an event internally and returns from the execution context.
        /// </summary>
        /// <param name="e">Event</param>
        protected internal void Raise(Event e)
        {
            Utilities.Verbose("Machine {0} raised event {1}\n", this, e);
            State currentState = this.StateStack.Peek();
            this.HandleEvent(currentState, e);
        }

        /// <summary>
        /// Pops the current state from the push state stack.
        /// </summary>
        protected internal void Return()
        {
            throw new ReturnUsedException(this.StateStack.Pop());
        }

        /// <summary>
        /// Checks if the assertion holds, and if not it reports
        /// an error and exits.
        /// </summary>
        /// <param name="predicate">Predicate</param>
        protected internal void Assert(bool predicate)
        {
            Runtime.Assert(predicate);
        }

        /// <summary>
        /// Checks if the assertion holds, and if not it reports
        /// an error and exits.
        /// </summary>
        /// <param name="predicate">Predicate</param>
        /// <param name="s">Message</param>
        /// <param name="args">Message arguments</param>
        protected internal void Assert(bool predicate, string s, params object[] args)
        {
            Runtime.Assert(predicate, s, args);
        }

        #endregion

        #region factory methods

        /// <summary>
        /// Factory class for creating machines.
        /// </summary>
        public static class Factory
        {
            /// <summary>
            /// Creates a new machine of type T with an optional payload.
            /// </summary>
            /// <typeparam name="T">Type of machine</typeparam>
            /// <param name="payload">Optional payload</param>
            /// <returns>Machine</returns>
            public static T Create<T>(params Object[] payload)
            {
                Runtime.Assert(typeof(T).IsSubclassOf(typeof(Machine)), "Type '{0}' is " +
                    "not a subclass of Machine.\n", typeof(T).Name);
                var machine = Runtime.TryCreateNewMachineInstance<T>(payload);
                return machine;
            }

            /// <summary>
            /// Creates a new machine of type T with an optional payload.
            /// </summary>
            /// <param name="m">Type of machine</param>
            /// <param name="payload">Optional payload</param>
            /// <returns>Machine</returns>
            internal static Machine Create(Type m, params Object[] payload)
            {
                Runtime.Assert(m.IsSubclassOf(typeof(Machine)), "Type '{0}' is not " +
                    "a subclass of Machine.\n", m.Name);
                var machine = Runtime.TryCreateNewMachineInstance(m, payload);
                return machine;
            }

            /// <summary>
            /// Creates a new monitor of type T with an optional payload.
            /// </summary>
            /// <typeparam name="T">Type of monitor</typeparam>
            /// <param name="payload">Optional payload</param>
            public static void CreateMonitor<T>(params Object[] payload)
            {
                Runtime.Assert(typeof(T).IsSubclassOf(typeof(Machine)), "Type '{0}' is not " +
                    "a subclass of Machine.\n", typeof(T).Name);
                Runtime.Assert(typeof(T).IsDefined(typeof(Monitor), false), "Type '{0}' is " +
                    "not a monitor.\n", typeof(T).Name);
                Runtime.TryCreateNewMonitorInstance<T>(payload);
            }

            /// <summary>
            /// Creates a new monitor of type T with an optional payload.
            /// </summary>
            /// <param name="m">Type of monitor</param>
            /// <param name="payload">Optional payload</param>
            internal static void CreateMonitor(Type m, params Object[] payload)
            {
                Runtime.Assert(m.IsSubclassOf(typeof(Machine)), "Type '{0}' is not a " +
                    "subclass of Machine.\n", m.Name);
                Runtime.Assert(m.IsDefined(typeof(Monitor), false), "Type '{0}' is not " +
                    "a monitor.\n", m.Name);
                Runtime.TryCreateNewMonitorInstance(m, payload);
            }
        }

        #endregion

        #region P# internal methods

        /// <summary>
        /// Starts the machine concurrently with an optional payload.
        /// </summary>
        /// /// <param name="payload">Optional payload</param>
        internal void Start(params Object[] payload)
        {
            lock (this.Lock)
            {
                this.GotoInitialState(payload);
            }
        }

        /// <summary>
        /// Starts the machine concurrently with an optional payload and
        /// the scheduler enabled.
        /// </summary>
        /// /// <param name="payload">Optional payload</param>
        /// <returns>Task</returns>
        internal Thread ScheduledStart(params Object[] payload)
        {
            Thread thread = new Thread((Object pl) =>
            {
                ThreadInfo currThread = Runtime.Scheduler.GetCurrentThreadInfo();
                Runtime.Scheduler.ThreadStarted(currThread);

                try
                {
                    if (Runtime.Scheduler.DeadlockHasOccurred)
                    {
                        throw new TaskCanceledException();
                    }

                    this.GotoInitialState((Object[])pl);

                    //while (this.IsActive)
                    //{
                    //    if (this.RaisedEvent != null)
                    //    {
                    //        Event nextEvent = this.RaisedEvent;
                    //        this.RaisedEvent = null;
                    //        this.HandleEvent(nextEvent);
                    //    }
                    //    else
                    //    {
                    //        // We are using a blocking collection so the attempt to
                    //        // dequeue an event will block if there is no available
                    //        // event in the mailbox. The operation will unblock when
                    //        // the next event arrives.
                    //        Event nextEvent = this.ScheduledInbox.Take();
                    //        this.HandleEvent(nextEvent);
                    //    }
                    //}
                }
                catch (TaskCanceledException) { }

                Runtime.Scheduler.ThreadEnded(currThread);
            });

            ThreadInfo threadInfo = Runtime.Scheduler.AddNewThreadInfo(thread);

            thread.Start(payload);

            Runtime.Scheduler.WaitForThreadToStart(threadInfo);

            return thread;
        }

        /// <summary>
        /// Enqueues an event.
        /// </summary>
        /// <param name="e">Event</param>
        /// <param name="sender">Sender machine</param>
        internal void Enqueue(Event e, string sender)
        {
            if (Runtime.Options.Mode == Runtime.Mode.Execution)
            {
                lock (this.Lock)
                {
                    if (!this.IsHalted)
                    {
                        this.Inbox.Add(e);
                        State currentState = this.StateStack.Peek();
                        Event nextEvent = this.DequeueNextEvent(currentState);
                        this.HandleEvent(currentState, nextEvent);
                    }
                }
            }
            else if (Runtime.Options.Mode == Runtime.Mode.BugFinding)
            {
                this.ScheduledInbox.Add(e);
                ScheduleExplorer.Add(sender, this.GetType().Name, e.GetType().Name);
            }
        }

        /// <summary>
        /// Checks if machine has terminated.
        /// </summary>
        /// <returns></returns>
        internal bool IsTerminated()
        {
            lock (this.Lock)
            {
                return this.IsHalted;
            }
        }

        /// <summary>
        /// Stop listening to events.
        /// </summary>
        internal void StopListener()
        {
            this.IsActive = false;

            if (Runtime.Options.Mode == Runtime.Mode.Execution)
                this.CTS.Cancel();
            else if (Runtime.Options.Mode == Runtime.Mode.BugFinding)
                this.ScheduledInbox.Cancel();
        }

        /// <summary>
        /// Resets the machine ID counter.
        /// </summary>
        internal static void ResetMachineIDCounter()
        {
            Machine.IdCounter = 0;
        }

        #endregion

        #region private machine methods

        /// <summary>
        /// Dequeues the next available event. If no event is
        /// available returns null.
        /// </summary>
        /// <param name="currentState">Current state</param>
        /// <returns>Next event</returns>
        private Event DequeueNextEvent(State currentState)
        {
            Event nextEvent = null;

            if (this.Inbox.Count > 0)
            {
                // Iterate through the event in the inbox.
                for (int idx = 0; idx < this.Inbox.Count; idx++)
                {
                    // Remove any ignored events.
                    if (currentState.IsIgnored(this.Inbox[idx]))
                    {
                        this.Inbox.RemoveAt(idx);
                        if (idx == this.Inbox.Count)
                        {
                            break;
                        }
                    }

                    // Dequeue the first event that is not handled by the state,
                    // or is not deferred.
                    if (!currentState.CanHandleEvent(this.Inbox[idx]) ||
                        !currentState.IsDeferred(this.Inbox[idx]))
                    {
                        nextEvent = this.Inbox[idx];
                        this.Inbox.RemoveAt(idx);
                        break;
                    }
                }
            }

            return nextEvent;
        }

        /// <summary>
        /// Handles the given event.
        /// </summary>
        /// <param name="currentState">Current state</param>
        /// <param name="e">Event to handle</param>
        private void HandleEvent(State currentState, Event e)
        {
            if (e == null)
            {
                if (currentState.HasDefaultHandler())
                {
                    e = new Default();
                }
                else
                {
                    return;
                }
            }

            this.Message = e.GetType();
            this.Payload = e.Payload;

            while (true)
            {
                if (this.StateStack.Count == 0)
                {
                    // If the stack of states is empty and the event
                    // is halt, then terminate the machine.
                    if (e.GetType().Equals(typeof(Halt)))
                    {
                        Utilities.Verbose("{0}: HALT\n", this);
                        this.IsHalted = true;
                        this.CleanUpResources();
                        return;
                    }

                    // If the event cannot be handled then report an error and exit.
                    Runtime.Assert(false, "Machine '{0}' received event '{1}' that cannot be " +
                        "handled in state '{2}'.\n", this.GetType().Name, e.GetType().Name,
                        currentState.GetType().Name);
                }

                // If current state cannot handle the event then pop the state.
                if (!currentState.CanHandleEvent(e))
                {
                    this.StateStack.Pop();
                    continue;
                }

                // Checks if the event can trigger a goto state transition.
                if (currentState.ContainsGotoTransition(e))
                {
                    var transition = currentState.GetGotoTransition(e);
                    Type targetState = transition.Item1;
                    Action onExitAction = transition.Item2;
                    Utilities.Verbose("{0}: {1} --- GOTO ---> {2}\n",
                        this, currentState, targetState);
                    this.Goto(targetState, onExitAction);
                }
                // Checks if the event can trigger a push state transition.
                else if (currentState.ContainsPushTransition(e))
                {
                    Type targetState = currentState.GetPushTransition(e);
                    Utilities.Verbose("{0}: {1} --- PUSH ---> {2}\n",
                        this, currentState, targetState);
                    this.Push(targetState);
                }
                // Checks if the event can trigger an action.
                else if (currentState.ContainsActionBinding(e))
                {
                    Action action = currentState.GetActionBinding(e);
                    this.Do(action);
                }

                break;
            }

            if (!this.IsHalted)
            {
                this.Message = null;
                this.Payload = null;

                currentState = this.StateStack.Peek();
                Event nextEvent = this.DequeueNextEvent(currentState);
                this.HandleEvent(currentState, nextEvent);
            }
        }

        /// <summary>
        /// Initializes information about the states of the machine.
        /// </summary>
        private void InitializeStateInformation()
        {
            this.StateTypes = new HashSet<Type>();

            Type machineType = this.GetType();
            Type initialState = null;

            while (machineType != typeof(Machine))
            {
                foreach (var s in machineType.GetNestedTypes(BindingFlags.Instance |
                    BindingFlags.NonPublic | BindingFlags.Public |
                    BindingFlags.DeclaredOnly))
                {
                    if (s.IsClass && s.IsSubclassOf(typeof(State)))
                    {
                        if (s.IsDefined(typeof(Initial), false))
                        {
                            Runtime.Assert(initialState == null, "Machine '{0}' can not have " +
                                "more than one initial states.\n", this.GetType().Name);
                            initialState = s;
                        }

                        Runtime.Assert(s.BaseType == typeof(State), "State '{0}' is " +
                            "not of the correct type.\n", s.Name);
                        this.StateTypes.Add(s);
                    }
                }

                machineType = machineType.BaseType;
            }

            this.StateStack.Push(this.InitializeState(initialState));
        }

        /// <summary>
        /// Initializes a state of the given type.
        /// </summary>
        /// <param name="s">Type of the state</param>
        /// <returns>State</returns>
        private State InitializeState(Type s)
        {
            State state = State.Factory.CreateState(s);

            GotoStateTransitions sst = null;
            PushStateTransitions cst = null;
            ActionBindings ab = null;

            this.GotoTransitions.TryGetValue(s, out sst);
            this.PushTransitions.TryGetValue(s, out cst);
            this.ActionBindings.TryGetValue(s, out ab);

            state.Machine = this;
            state.InitializeState(sst, cst, ab);

            return state;
        }

        /// <summary>
        /// Executes the initial state with an optional payload.
        /// </summary>
        /// /// <param name="payload">Optional payload</param>
        private void GotoInitialState(params Object[] payload)
        {
            if (payload.Length == 0)
            {
                this.Payload = null;
            }
            else if (payload.Length == 1)
            {
                this.Payload = payload[0];
            }
            else
            {
                this.Payload = payload;
            }

            // Performs the on entry statements of the new state.
            this.ExecuteCurrentStateOnEntry();
        }

        /// <summary>
        /// Performs a goto transition to the given state.
        /// </summary>
        /// <param name="s">Type of the state</param>
        /// <param name="onExit">Goto on exit action</param>
        private void Goto(Type s, Action onExit)
        {
            State nextState = this.InitializeState(s);
            // The machine performs the on exit statements of the current state.
            this.ExecuteCurrentStateOnExit(onExit);
            // The machine transitions to the new state.
            this.StateStack.Pop();
            this.StateStack.Push(nextState);
            // The machine performs the on entry statements of the new state.
            this.ExecuteCurrentStateOnEntry();
        }

        /// <summary>
        /// Performs a push transition to the given state.
        /// </summary>
        /// <param name="s">Type of the state</param>
        private void Push(Type s)
        {
            State nextState = this.InitializeState(s);
            // The machine transitions to the new state.
            this.StateStack.Push(nextState);
            // The machine performs the on entry statements of the new state.
            this.ExecuteCurrentStateOnEntry();
        }

        /// <summary>
        /// Performs an action.
        /// </summary>
        /// <param name="a">Action</param>
        private void Do(Action a)
        {
            try
            {
                a();
            }
            catch (ReturnUsedException ex)
            {
                // Handles the returning state.
                this.AssertReturnStatementValidity(ex.ReturningState);
            }
            catch (TaskCanceledException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                // Handles generic exception.
                this.ReportGenericAssertion(ex);
            }
        }

        /// <summary>
        /// The machine handles the given event.
        /// </summary>
        /// <param name="e">Type of the event</param>
        //private void HandleEvent(Event e)
        //{
        //    State currentState = this.StateStack.Peek();
        //    this.Message = e.GetType();
        //    this.Payload = e.Payload;

        //    // Checks if the event can be handled by the current state.
        //    if (!currentState.CanHandleEvent(e))
        //    {
        //        // Checks if the event can be handled by any state in the push
        //        // state stack, if there are any. If it can be handled, then
        //        // the push state stack is manipulated appropriately.
        //        if (!this.TryFindStateThatCanHandleEvent(ref currentState, e))
        //        {
        //            Runtime.Assert(!(currentState.IgnoredEvents.Contains(e.GetType()) &&
        //                currentState.DeferredEvents.Contains(e.GetType())), "Machine '{0}' " +
        //                "received event '{1}' which is both ignored and deferred in state '{2}'.\n",
        //                this.GetType().Name, e.GetType().Name, currentState.GetType().Name);
        //            Runtime.Assert(currentState.IgnoredEvents.Contains(e.GetType()) ||
        //                currentState.DeferredEvents.Contains(e.GetType()), "Machine '{0}' " +
        //                "received event '{1}', which cannot be handled in state '{2}'.\n",
        //                this.GetType().Name, e.GetType().Name, currentState.GetType().Name);

        //            // If the event to process is in the ignored set of the current
        //            // state (or stack of states in case of a push transition), then
        //            // the event is removed from the inbox queue.
        //            if (currentState.IgnoredEvents.Contains(e.GetType()))
        //            {
        //                return;
        //            }
        //            // If the event to process is in the deferred set of the current
        //            // state (or stack of states in case of a push transition), then
        //            // the event processing is deferred and the machine will attempt
        //            // to process the next event in the inbox queue.
        //            else if (currentState.DeferredEvents.Contains(e.GetType()))
        //            {
        //                if (Runtime.Options.Mode == Runtime.Mode.Execution)
        //                    this.Inbox.Add(e);
        //                else if (Runtime.Options.Mode == Runtime.Mode.BugFinding)
        //                    this.ScheduledInbox.Add(e);
        //                return;
        //            }
        //        }
        //    }

        //    // If the event is neither in the ignored nor in the deferred set
        //    // of the current state, then the machine can process it. The
        //    // machine checks if the event triggers a goto state transition.
        //    if (currentState.ContainsGotoTransition(e))
        //    {
        //        var transition = currentState.GetGotoTransition(e);
        //        Type targetState = transition.Item1;
        //        Action onExitOverride = transition.Item2;
        //        Utilities.Verbose("{0}: {1} --- STEP ---> {2}\n",
        //            this, currentState, targetState);
        //        this.Goto(targetState, onExitOverride);
        //    }
        //    // If the event is neither in the ignored nor in the deferred set
        //    // of the current state, then the machine can process it. The
        //    // machine checks if the event triggers a push state transition.
        //    else if (currentState.ContainsPushTransition(e))
        //    {
        //        Type targetState = currentState.GetPushTransition(e);
        //        Utilities.Verbose("{0}: {1} --- CALL ---> {2}\n",
        //            this, currentState, targetState);
        //        this.Push(targetState);
        //    }
        //    // If the event is neither in the ignored nor in the deferred set
        //    // of the current state, then the machine can process it. The
        //    // machine checks if the event triggers an action.
        //    else if (currentState.ContainsActionBinding(e))
        //    {
        //        Action action = currentState.GetActionBinding(e);
        //        this.Do(action);
        //    }
        //}

        #endregion

        #region helper methods

        /// <summary>
        /// Executes the on entry function of the current state.
        /// </summary>
        private void ExecuteCurrentStateOnEntry()
        {
            try
            {
                // Performs the on entry statements of the new state.
                this.StateStack.Peek().ExecuteEntryFunction();
            }
            catch (ReturnUsedException ex)
            {
                // Handles the returning state.
                this.AssertReturnStatementValidity(ex.ReturningState);
            }
            catch (TaskCanceledException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                // Handles generic exception.
                this.ReportGenericAssertion(ex);
            }
        }

        /// <summary>
        /// Executes the on exit function of the current state.
        /// </summary>
        /// <param name="onExit">Goto on exit action</param>
        private void ExecuteCurrentStateOnExit(Action onExit)
        {
            try
            {
                // Performs the on exit statements of the current state.
                this.StateStack.Peek().ExecuteExitFunction();
                if (onExit != null)
                {
                    onExit();
                }
            }
            catch (ReturnUsedException ex)
            {
                // Handles the returning state.
                this.AssertReturnStatementValidity(ex.ReturningState);
            }
            catch (TaskCanceledException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                // Handles generic exception.
                this.ReportGenericAssertion(ex);
            }
        }

        #endregion

        #region generic public and override methods

        /// <summary>
        /// Determines whether the specified machine is equal
        /// to the current machine.
        /// </summary>
        /// <param name="m">Machine</param>
        /// <returns>Boolean value</returns>
        public bool Equals(Machine m)
        {
            if (m == null)
            {
                return false;
            }

            return this.Id == m.Id;
        }

        /// <summary>
        /// Determines whether the specified System.Object is equal
        /// to the current System.Object.
        /// </summary>
        /// <param name="obj">Object</param>
        /// <returns>Boolean value</returns>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            Machine m = obj as Machine;
            if (m == null)
            {
                return false;
            }

            return this.Id == m.Id;
        }

        /// <summary>
        /// Hash function.
        /// </summary>
        /// <returns>int</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Returns a string that represents the current machine.
        /// </summary>
        /// <returns>string</returns>
        public override string ToString()
        {
            return this.GetType().Name;
        }

        #endregion

        #region error checking

        /// <summary>
        /// Check machine for state related errors.
        /// </summary>
        private void AssertStateValidity()
        {
            Runtime.Assert(this.StateTypes.Count > 0, "Machine '{0}' must " +
                "have one or more states.\n", this.GetType().Name);
            Runtime.Assert(this.StateStack.Peek() != null, "Machine '{0}' " +
                "must not have a null current state.\n", this.GetType().Name);
        }

        /// <summary>
        /// Checks if the Return() statement was performed properly.
        /// </summary>
        /// <param name="returningState">Returnig state</param>
        private void AssertReturnStatementValidity(State returningState)
        {
            Runtime.Assert(this.StateStack.Count > 0, "Machine '{0}' executed a Return() " +
                "statement while there was only the state '{1}' in the stack.\n",
                this.GetType().Name, returningState.GetType().Name);
        }

        /// <summary>
        /// Reports the generic assertion and raises a generic
        /// runtime assertion error.
        /// </summary>
        /// <param name="ex">Exception</param>
        private void ReportGenericAssertion(Exception ex)
        {
            Runtime.Assert(false, "Exception '{0}' was thrown in machine '{1}'. The stack " +
                "trace is:\n{2}\n", ex.GetType(), this.GetType().Name, ex.StackTrace);
        }

        #endregion

        #region cleanup methods

        /// <summary>
        /// Cleans up resources at machine termination.
        /// </summary>
        private void CleanUpResources()
        {
            this.StateTypes.Clear();
            this.GotoTransitions.Clear();
            this.PushTransitions.Clear();
            this.ActionBindings.Clear();
            this.Inbox.Clear();

            this.Message = null;
            this.Payload = null;
    }

        #endregion
    }
}
