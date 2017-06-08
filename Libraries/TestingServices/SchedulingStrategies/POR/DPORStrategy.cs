using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PSharp.TestingServices.Scheduling.POR
{
    /// <summary>
    /// Dynamic partial-order reduction strategy.
    /// </summary>
    public class DPORStrategy : ISchedulingStrategy
    {
        private readonly Stack Stack;
        private readonly DPORAlgorithm Dpor;
        private readonly bool SleepSets;


        /// <summary>
        /// Dynamic partial-order reduction strategy.
        /// </summary>
        public DPORStrategy(bool dpor, bool sleepSets)
        {
            Stack = new Stack();
            Dpor = dpor ? new DPORAlgorithm() : null;
            SleepSets = sleepSets;
            Reset();
        }

        /// <summary>
        /// Prepares the next scheduling iteration.
        /// </summary>
        public void ConfigureNextIteration()
        {
            // Do nothing.
        }

        /// <summary>
        /// Returns a textual description of the scheduling strategy.
        /// </summary>
        /// <returns>String</returns>
        public string GetDescription()
        {
            return "DPOR";
        }

        /// <summary>
        /// Returns the explored steps.
        /// </summary>
        /// <returns>Explored steps</returns>
        public int GetExploredSteps()
        {
            return Stack.GetNumSteps();
        }

        /// <summary>
        /// Returns the next boolean choice.
        /// </summary>
        /// <param name="maxValue">Max value</param>
        /// <param name="next">Next</param>
        /// <returns>Boolean</returns>
        public bool GetNextBooleanChoice(int maxValue, out bool next)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the next integer choice.
        /// </summary>
        /// <param name="maxValue">Max value</param>
        /// <param name="next">Next</param>
        /// <returns>Boolean</returns>
        public bool GetNextIntegerChoice(int maxValue, out int next)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// True if the scheduling has finished.
        /// </summary>
        /// <returns>Boolean</returns>
        public bool HasFinished()
        {
            Dpor?.DoDPOR(Stack);

            Stack.PrepareForNextSchedule();
            return Stack.GetInternalSize() == 0;
        }

        /// <summary>
        /// True if the scheduling strategy has reached the max
        /// scheduling steps for the given scheduling iteration.
        /// </summary>
        /// <returns>Boolean</returns>
        public bool HasReachedMaxSchedulingSteps()
        {
            return false;
        }

        /// <summary>
        /// Checks if this a fair scheduling strategy.
        /// </summary>
        /// <returns>Boolean</returns>
        public bool IsFair()
        {
            return false;
        }

        /// <summary>
        /// Resets the scheduling strategy.
        /// </summary>
        public void Reset()
        {
            Stack.Clear();
        }

        /// <summary>
        /// Returns the next machine to schedule.
        /// </summary>
        /// <param name="next">Next</param>
        /// <param name="choices">Choices</param>
        /// <param name="current">Curent</param>
        /// <returns>Boolean</returns>
        public bool TryGetNext(out MachineInfo next, IEnumerable<MachineInfo> choices, MachineInfo current)
        {
            List<MachineInfo> choicesList = choices.ToList();

            bool added = Stack.Push(choicesList, current.Id);

            if (added)
            {
                TidEntryList top = Stack.GetTop();

                if (Dpor == null)
                {
                    top.SetAllEnabledToBeBacktracked();
                }

                if (SleepSets)
                {
                    // TODO: update sleep set.
                }

            }

            int nextTidIndex = Stack.GetSelectedOrFirstBacktrackNotSlept(current.Id);

            if (nextTidIndex < 0)
            {
                next = null;
                return false;
            }

            TidEntry nextTidEntry = Stack.GetTop().List[nextTidIndex];

            if (!nextTidEntry.Selected)
            {
                nextTidEntry.Selected = true;
            }

            next = choicesList[nextTidEntry.Id];
            return true;
        }
    }
}