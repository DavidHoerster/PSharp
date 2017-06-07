using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PSharp.TestingServices.Scheduling.POR
{
    /// <summary>
    /// 
    /// </summary>
    public class Stack
    {
        /// <summary>
        /// The actual stack.
        /// </summary>
        public readonly List<TidEntryList> StackInternal = new List<TidEntryList>();

        private int nextStackPos;

        /// <summary>
        /// Push a list of tid entries onto the stack.
        /// </summary>
        /// <param name="machines"></param>
        /// <param name="prevThreadIndex"></param>
        /// <returns></returns>
        /// <exception cref="RuntimeException"></exception>
        public bool Push(List<MachineInfo> machines, int prevThreadIndex)
        {
            List<TidEntry> list = new List<TidEntry>();

            int i = prevThreadIndex;
            int threadCount = machines.Count;
            for (int count = 0; count < threadCount; ++count)
            {
                MachineInfo machineInfo = machines[i];
                list.Add(
                    new TidEntry(
                        machineInfo.Id,
                        machineInfo.IsEnabled, 
                        machineInfo.NextOperationType, 
                        machineInfo.NextTargetId));
                ++i;
                if (i >= threadCount)
                {
                    i = 0;
                }
            }

            if (nextStackPos > StackInternal.Count)
            {
                throw new RuntimeException("DFS strategy unexpected stack state.");
            }

            bool added = nextStackPos == StackInternal.Count;

            if (added)
            {
                StackInternal.Add(new TidEntryList(list));
            }
            else
            {
                CheckMatches(list);
            }
            ++nextStackPos;

            return added;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int GetNumSteps()
        {
            return nextStackPos;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int GetInternalSize()
        {
            return StackInternal.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public TidEntryList GetTop()
        {
            return StackInternal[nextStackPos - 1];
        }

        /// <summary>
        /// Gets the top of stack and also ensures that this is the real top of stack.
        /// </summary>
        /// <returns></returns>
        public TidEntryList GetTopAsRealTop()
        {
            if (nextStackPos != StackInternal.Count)
            {
                throw new RuntimeException("DFS Strategy: top of stack is not aligned.");
            }
            return GetTop();
        }

        /// <summary>
        /// Get the next thread to schedule: either the preselected thread entry
        /// from the current schedule prefix that we are replaying or the first
        /// suitable thread entry from the real top of the stack.
        /// </summary>
        /// <returns></returns>
        public int GetSelectedOrFirstEnabledNotSlept()
        {
            var top = GetTop();

            return StackInternal.Count == nextStackPos
                ? top.GetFirstTidNotSlept()
                : top.GetSelected();
        }

        /// <summary>
        /// 
        /// </summary>
        public void PrepareForNextSchedule()
        {
            // Deadlock / sleep set blocked; no selected tid entry.
            {
                TidEntryList top = GetTopAsRealTop();
                
                if (top.IsNoneSelected())
                {
                    Pop();
                }
            }
            

            // Pop until there are some tid entries that are not slept OR stack is empty.
            while (StackInternal.Count > 0)
            {
                TidEntryList top = GetTopAsRealTop();
                top.SetSelectedToSleep();
                top.ClearSelected();

                if (!top.AllSlept())
                {
                    break;
                }

                Pop();
            }

            nextStackPos = 0;
        }

        private void Pop()
        {
            if (nextStackPos != StackInternal.Count)
            {
                throw new RuntimeException("DFS Strategy: top of stack is not aligned.");
            }
            StackInternal.RemoveAt(StackInternal.Count - 1);
            --nextStackPos;
        }

        private void CheckMatches(List<TidEntry> list)
        {
            if (!StackInternal[nextStackPos].List.SequenceEqual(list, TidEntry.ComparerSingleton))
            {
                throw new RuntimeException("DFS strategy detected nondeterminism.");
            }

        }
    }
}