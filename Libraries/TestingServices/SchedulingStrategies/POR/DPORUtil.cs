﻿using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PSharp.TestingServices.Scheduling.POR
{
    /// <summary>
    /// 
    /// </summary>
    public class DPORUtil
    {
        private uint numThreads;
        private uint numSteps;
        
        private uint[] threadIdToLastOpIndex; 
        private uint[] targetIdToLastAccess;
        // List of prior sends that have not yet been received.
        private List<uint>[] targetIdToListOfSends;
        private uint[] vcs;

        /// <summary>
        /// 
        /// </summary>
        public DPORUtil()
        {
            // inital estimates

            numThreads = 4;
            numSteps = 1 << 8;

            threadIdToLastOpIndex = new uint[numThreads];
            targetIdToLastAccess = new uint[numThreads];
            targetIdToListOfSends = new List<uint>[numThreads];
            vcs = new uint[numSteps * numThreads];
        }


        private void FromVCSetVC(uint from, uint to)
        {
            uint fromI = (from-1) * numThreads;
            uint toI = (to-1) * numThreads;
            for (uint i = 0; i < numThreads; ++i)
            {
                vcs[toI] = vcs[fromI];
                ++fromI;
                ++toI;
            }
        }

        private void ForVCSetClockToValue(uint vc, uint clock, uint value)
        {
            vcs[(vc - 1) * numThreads + clock] = value;
        }

        private uint ForVCGetClock(uint vc, int clock)
        {
            return vcs[(vc - 1) * numThreads + clock];
        }

        private uint[] GetVC(uint vc)
        {
            uint[] res = new uint[numThreads];
            uint fromI = (vc - 1) * numThreads;
            for (uint i = 0; i < numThreads; ++i)
            {
                res[i] = vcs[fromI];
                ++fromI;
            }
            return res;
        }

        private bool HB(Stack stack, uint vc1, uint vc2)
        {
            // A hb B
            // iff:
            // A's index <= B.VC[A's tid]

            TidEntry aStep = GetSelectedTidEntry(stack, vc1 - 1);

            return vc1 <= ForVCGetClock(vc2, aStep.Id);
        }


        private TidEntry GetSelectedTidEntry(Stack stack, uint index)
        {
            var list = GetThreadsAt(stack, index);
            return list.List[list.GetSelected()];
        }

        /// <summary>
        /// Assumes both operations passed in are dependent.
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="index1"></param>
        /// <param name="index2"></param>
        /// <returns></returns>
        private bool Reversible(Stack stack, uint index1, uint index2)
        {
            var step1 = GetSelectedTidEntry(stack, index1);
            var step2 = GetSelectedTidEntry(stack, index2);
            return step1.OpType == OperationType.Send &&
                   step2.OpType == OperationType.Send;
        }

        private static TidEntryList GetThreadsAt(Stack stack, uint index)
        {
            return stack.StackInternal[(int)index - 1];
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="stack"></param>
        public void DoDPOR(Stack stack)
        {
            UpdateFieldsAndRealocateDatastructuresIfNeeded(stack);
            
            // Indexes start at 1.

            for (uint i = 1; i < numSteps; ++i)
            {
                TidEntry step = GetSelectedTidEntry(stack, i);

                FromVCSetVC(threadIdToLastOpIndex[step.Id], i);
                ForVCSetClockToValue(i, (uint) step.Id, i);

                uint lastAccessIndex = targetIdToLastAccess[step.TargetId];
                // special case for receives:
                if (step.OpType == OperationType.Receive)
                {
                    var listOfSends = targetIdToListOfSends[step.TargetId];
                    lastAccessIndex = listOfSends[0];
                    listOfSends.RemoveAt(0);
                }

                AddBacktrack(stack, lastAccessIndex, i, step);

            }

        }

        private void AddBacktrack(Stack stack,
            uint lastAccessIndex,
            uint i,
            TidEntry step)
        {
            if (lastAccessIndex <= 0 ||
                HB(stack, lastAccessIndex, i) ||
                !Reversible(stack, lastAccessIndex, i)) return;

            // Race between `a` and `b`.
            // Must find first steps after `a` that do not HA `a`
            // (except maybe b) and do not HA each other.
            // candidates = {}
            // if b.tid is enabled before a:
            //   add b.tid to candidates
            // notYetFound = set of enabled threads before a - a.tid - b.tid.
            // let vc = [0,0,...]
            // vc[a.tid] = a;
            // for k = aIndex+1 to bIndex:
            //   if notYetFound does not contain k.tid:
            //     continue
            //   remove k.tid from notYetFound
            //   doesHaAnother = false
            //   foreach t in tids:
            //     if vc[t] hb k:
            //       doesHaAnother = true
            //       break
            //   vc[k.tid] = k
            //   if !doesHaAnother:
            //     add k.tid to candidates
            //       
            //   vc = vc n k.vc


            var candidateThreadIds = new HashSet<uint>();
            var a = GetSelectedTidEntry(stack, lastAccessIndex);
            var beforeA = GetThreadsAt(stack, lastAccessIndex - 1);
            if (beforeA.List[step.Id].Enabled)
            {
                candidateThreadIds.Add((uint) step.Id);
            }
            var notYetFound = new HashSet<uint>();
            for (uint j = 0; j < beforeA.List.Count; ++j)
            {
                if (j != a.Id &&
                    j != step.Id &&
                    beforeA.List[(int) j].Enabled)
                {
                    notYetFound.Add(j);
                }
            }

            uint[] vc = new uint[numThreads];
            vc[a.Id] = lastAccessIndex;
            for (uint k = lastAccessIndex + 1; k < i; ++k)
            {
                var kEntry = GetSelectedTidEntry(stack, k);
                if (!notYetFound.Contains((uint) kEntry.Id)) continue;

                notYetFound.Remove((uint) kEntry.Id);
                bool doesHaAnother = false;
                for (int t = 0; t < numThreads; ++t)
                {
                    if (vc[t] > 0 &&
                        vc[t] <= ForVCGetClock(k, t))
                    {
                        doesHaAnother = true;
                        break;
                    }
                }
                if (!doesHaAnother)
                {
                    candidateThreadIds.Add((uint) kEntry.Id);
                }
                if (notYetFound.Count == 0)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stack"></param>
        private void UpdateFieldsAndRealocateDatastructuresIfNeeded(Stack stack)
        {
            numThreads = (uint) stack.GetTopAsRealTop().List.Count;
            numSteps = (uint) stack.StackInternal.Count;

            int temp = threadIdToLastOpIndex.Length;
            while (temp < numThreads)
            {
                temp <<= 1;
            }

            if (threadIdToLastOpIndex.Length < temp)
            {
                threadIdToLastOpIndex = new uint[temp];
                targetIdToLastAccess = new uint[temp];
                targetIdToListOfSends = new List<uint>[temp];
            }

            uint numClocks = numThreads * numSteps;

            temp = vcs.Length;

            while (temp < numClocks)
            {
                temp <<= 1;
            }

            if (vcs.Length < temp)
            {
                vcs = new uint[temp];
            }
        }

    }
}