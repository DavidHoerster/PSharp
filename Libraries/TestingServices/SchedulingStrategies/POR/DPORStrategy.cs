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
        private Stack stack;


        /// <summary>
        /// Dynamic partial-order reduction strategy.
        /// </summary>
        public DPORStrategy()
        {
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
            return stack.GetNumSteps();
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
            stack.PrepareForNextSchedule();
            return stack.GetInternalSize() == 0;
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
            stack = new Stack();
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

            bool added = stack.Push(choicesList, current.Id);

            if (added)
            {
                TidEntryList top = stack.GetTop();

                // TODO: update sleep set.

            }

            int nextTidIndex = stack.GetSelectedOrFirstBacktrackNotSlept(current.Id);

            if (nextTidIndex < 0)
            {
                next = null;
                return false;
            }

            TidEntry nextTidEntry = stack.GetTop().List[nextTidIndex];

            if (!nextTidEntry.Selected)
            {
                nextTidEntry.Selected = true;
            }

            next = choicesList[nextTidEntry.Id];
            return true;
        }
    }
}