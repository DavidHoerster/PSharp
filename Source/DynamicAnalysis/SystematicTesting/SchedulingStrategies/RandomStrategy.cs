﻿//-----------------------------------------------------------------------
// <copyright file="RandomStrategy.cs" company="Microsoft">
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

using Microsoft.PSharp.Scheduling;
using Microsoft.PSharp.Tooling;

namespace Microsoft.PSharp.DynamicAnalysis.Scheduling
{
    /// <summary>
    /// Class representing a random delay scheduling strategy.
    /// </summary>
    public class RandomStrategy : ISchedulingStrategy
    {
        #region fields

        /// <summary>
        /// The analysis context.
        /// </summary>
        protected AnalysisContext AnalysisContext;

        /// <summary>
        /// Nondeterminitic seed.
        /// </summary>
        private int Seed;

        /// <summary>
        /// Randomizer.
        /// </summary>
        private Random Random;

        /// <summary>
        /// The number of explored scheduling steps.
        /// </summary>
        private int SchedulingSteps;

        #endregion

        #region public API

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">AnalysisContext</param>
        public RandomStrategy(AnalysisContext context)
        {
            this.AnalysisContext = context;
            this.Seed = this.AnalysisContext.Configuration.RandomSchedulingSeed
                ?? DateTime.Now.Millisecond;
            this.SchedulingSteps = 0;
            this.Random = new Random(this.Seed);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">AnalysisContext</param>
        /// <param name="seed">Scheduling steps</param>
        public RandomStrategy(AnalysisContext context, int steps)
        {
            this.AnalysisContext = context;
            this.Seed = this.AnalysisContext.Configuration.RandomSchedulingSeed
                ?? DateTime.Now.Millisecond;
            this.SchedulingSteps = steps;
            this.Random = new Random(this.Seed);
        }

        /// <summary>
        /// Returns the next task to schedule.
        /// </summary>
        /// <param name="next">Next</param>
        /// <param name="tasks">Tasks</param>
        /// <param name="currentTask">Curent task</param>
        /// <returns>Boolean value</returns>
        public bool TryGetNext(out TaskInfo next, List<TaskInfo> tasks, TaskInfo currentTask)
        {
            var enabledTasks = tasks.Where(task => task.IsEnabled).ToList();
            if (enabledTasks.Count == 0)
            {
                next = null;
                return false;
            }

            var availableTasks = enabledTasks.Where(
                task => !task.IsBlocked && !task.IsWaiting).ToList();
            if (availableTasks.Count == 0)
            {
                next = null;
                return false;
            }

            int id = this.Random.Next(availableTasks.Count);
            next = availableTasks[id];

            if (!currentTask.IsCompleted)
            {
                this.SchedulingSteps++;
            }

            return true;
        }

        /// <summary>
        /// Returns the next choice.
        /// </summary>
        /// <param name="next">Next</param>
        /// <returns>Boolean value</returns>
        public bool GetNextChoice(out bool next)
        {
            next = false;
            if (this.Random.Next(2) == 1)
            {
                next = true;
            }

            return true;
        }

        /// <summary>
        /// Returns the explored scheduling steps.
        /// </summary>
        /// <returns>Scheduling steps</returns>
        public int GetSchedulingSteps()
        {
            return this.SchedulingSteps;
        }

        /// <summary>  
        /// Returns the depth bound.
        /// </summary> 
        /// <returns>Depth bound</returns>  
        public int GetDepthBound()
        {
            return this.AnalysisContext.Configuration.DepthBound;
        }

        /// <summary>
        /// True if the scheduling strategy reached the depth bound
        /// for the given scheduling iteration.
        /// </summary>
        /// <returns>Depth bound</returns>
        public bool HasReachedDepthBound()
        {
            if (this.AnalysisContext.Configuration.DepthBound == 0)
            {
                return false;
            }

            return this.SchedulingSteps == this.GetDepthBound();
        }

        /// <summary>
        /// Returns true if the scheduling has finished.
        /// </summary>
        /// <returns>Boolean value</returns>
        public bool HasFinished()
        {
            return false;
        }
        
        /// <summary>
        /// Configures the next scheduling iteration.
        /// </summary>
        public void ConfigureNextIteration()
        {
            this.SchedulingSteps = 0;
        }

        /// <summary>
        /// Resets the scheduling strategy.
        /// </summary>
        public void Reset()
        {
            this.SchedulingSteps = 0;
        }

        /// <summary>
        /// Returns a textual description of the scheduling strategy.
        /// </summary>
        /// <returns>String</returns>
        public string GetDescription()
        {
            return "Random (with seed " + this.Seed + ")";
        }

        #endregion
    }
}
