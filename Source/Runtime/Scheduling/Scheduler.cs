﻿//-----------------------------------------------------------------------
// <copyright file="Scheduler.cs" company="Microsoft">
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

using Microsoft.PSharp.Tooling;

namespace Microsoft.PSharp.BugFinding
{
    /// <summary>
    /// Class implementing the P# bug-finding scheduler.
    /// </summary>
    internal sealed class Scheduler
    {
        #region fields

        /// <summary>
        /// The scheduling strategy to be used for bug-finding.
        /// </summary>
        private ISchedulingStrategy Strategy;

        /// <summary>
        /// List of tasks to schedule.
        /// </summary>
        private List<TaskInfo> Tasks;

        /// <summary>
        /// Map from task ids to task infos.
        /// </summary>
        private Dictionary<int, TaskInfo> TaskMap;

        /// <summary>
        /// True if a bug was found.
        /// </summary>
        internal bool BugFound
        {
            get; private set;
        }

        /// <summary>
        /// Number of scheduling points.
        /// </summary>
        internal int SchedulingPoints
        {
            get; private set;
        }

        #endregion

        #region internal scheduling methods

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="strategy">SchedulingStrategy</param>
        internal Scheduler(ISchedulingStrategy strategy)
        {
            this.Strategy = strategy;
            this.Tasks = new List<TaskInfo>();
            this.TaskMap = new Dictionary<int, TaskInfo>();
            this.BugFound = false;
            this.SchedulingPoints = 0;
        }

        /// <summary>
        /// Schedules the next machine to execute.
        /// </summary>
        /// <param name="id">TaskId</param>
        internal void Schedule(int? id)
        {
            var taskInfo = this.TaskMap[(int)id];

            TaskInfo next = null;
            if (!this.Strategy.TryGetNext(out next, this.Tasks))
            {
                Output.WriteSchedule("<ScheduleLog> Schedule explored.");
                this.KillRemainingTasks();
                return;
            }

            Output.WriteSchedule("<ScheduleLog> Schedule task {0} of machine {1}({2}).",
                next.Id, next.Machine.GetType(), next.Machine.Id);

            if (!taskInfo.IsCompleted)
            {
                this.SchedulingPoints++;
            }

            if (taskInfo != next)
            {
                taskInfo.IsActive = false;
                lock (next)
                {
                    next.IsActive = true;
                    System.Threading.Monitor.PulseAll(next);
                }

                lock (taskInfo)
                {
                    if (taskInfo.IsCompleted)
                    {
                        return;
                    }
                    
                    while (!taskInfo.IsActive)
                    {
                        Output.Debug("<ScheduleDebug> Sleep task {0} of machine {1}({2}) at schedule.",
                            taskInfo.Id, taskInfo.Machine.GetType(), taskInfo.Machine.Id);
                        System.Threading.Monitor.Wait(taskInfo);
                        Output.Debug("<ScheduleDebug> Wake up task {0} of machine {1}({2}) at schedule.",
                            taskInfo.Id, taskInfo.Machine.GetType(), taskInfo.Machine.Id);
                    }

                    if (!taskInfo.IsEnabled)
                    {
                        throw new TaskCanceledException();
                    }
                }
            }
        }

        /// <summary>
        /// Notify that a new task has been created for the given machine.
        /// </summary>
        /// <param name="id">TaskId</param>
        /// <param name="machine">Machine</param>
        internal void NotifyNewTaskCreated(int id, Machine machine)
        {
            var taskInfo = new TaskInfo(id, machine);

            Output.Debug("<ScheduleDebug> Created task {0} for machine {1}({2}).",
                taskInfo.Id, taskInfo.Machine.GetType(), taskInfo.Machine.Id);

            if (this.Tasks.Count == 0)
            {
                taskInfo.IsActive = true;
            }

            this.TaskMap.Add(id, taskInfo);
            this.Tasks.Add(taskInfo);
        }

        /// <summary>
        /// Notify that the task has started.
        /// </summary>
        /// <param name="id">TaskId</param>
        internal void NotifyTaskStarted(int? id)
        {
            if (id == null)
            {
                return;
            }

            var taskInfo = this.TaskMap[(int)id];

            Output.Debug("<ScheduleDebug> Started task {0} of machine {1}({2}).",
                taskInfo.Id, taskInfo.Machine.GetType(), taskInfo.Machine.Id);

            lock (taskInfo)
            {
                taskInfo.HasStarted = true;
                System.Threading.Monitor.PulseAll(taskInfo);
                while (!taskInfo.IsActive)
                {
                    Output.Debug("<ScheduleDebug> Sleep task {0} of machine {1}({2}).",
                        taskInfo.Id, taskInfo.Machine.GetType(), taskInfo.Machine.Id);
                    System.Threading.Monitor.Wait(taskInfo);
                    Output.Debug("<ScheduleDebug> Wake up task {0} of machine {1}({2}).",
                        taskInfo.Id, taskInfo.Machine.GetType(), taskInfo.Machine.Id);
                }

                if (!taskInfo.IsEnabled)
                {
                    throw new TaskCanceledException();
                }
            }
        }

        /// <summary>
        /// Notify that the task has completed.
        /// </summary>
        /// <param name="id">TaskId</param>
        internal void NotifyTaskCompleted(int? id)
        {
            if (id == null)
            {
                return;
            }
            
            var taskInfo = this.TaskMap[(int)id];

            Output.Debug("<ScheduleDebug> Completed task {0} of machine {1}({2}).",
                taskInfo.Id, taskInfo.Machine.GetType(), taskInfo.Machine.Id);

            taskInfo.IsEnabled = false;
            taskInfo.IsCompleted = true;

            this.Schedule(taskInfo.Id);

            Output.Debug("<ScheduleDebug> Exit task {0} of machine {1}({2}).",
                taskInfo.Id, taskInfo.Machine.GetType(), taskInfo.Machine.Id);
        }

        /// <summary>
        /// Notify that an assertion has failed.
        /// </summary>
        internal void NotifyAssertionFailure()
        {
            this.BugFound = true;
            this.KillRemainingTasks();
            throw new TaskCanceledException();
        }

        /// <summary>
        /// Wait for the task to start.
        /// </summary>
        /// <param name="id">TaskId</param>
        internal void WaitForTaskToStart(int id)
        {
            var taskInfo = this.TaskMap[id];
            lock (taskInfo)
            {
                while (!taskInfo.HasStarted)
                {
                    System.Threading.Monitor.Wait(taskInfo);
                }
            }
        }

        /// <summary>
        /// Checks if there is already an enabled task for the given machine.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <returns>Boolean</returns>
        internal bool HasEnabledTaskForMachine(Machine machine)
        {
            var enabledTasks = this.Tasks.Where(task => task.IsEnabled).ToList();
            return enabledTasks.Any(task => task.Machine.Equals(machine));
        }

        #endregion

        #region private scheduling methods

        /// <summary>
        /// Kills any remaining tasks at the end of the schedule.
        /// </summary>
        private void KillRemainingTasks()
        {
            foreach (var task in this.Tasks)
            {
                task.IsActive = true;
                task.IsEnabled = false;

                if (!task.IsCompleted)
                {
                    lock (task)
                    {
                        System.Threading.Monitor.PulseAll(task);
                    }
                }
            }
        }

        #endregion
    }
}
