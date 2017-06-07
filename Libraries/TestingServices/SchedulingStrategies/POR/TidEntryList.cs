using System.Collections.Generic;

namespace Microsoft.PSharp.TestingServices.Scheduling.POR
{
    /// <summary>
    /// 
    /// </summary>
    public class TidEntryList
    {
        /// <summary>
        /// 
        /// </summary>
        public readonly List<TidEntry> List;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="list"></param>
        public TidEntryList(List<TidEntry> list)
        {
            List = list;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int GetFirstTidNotSlept()
        {
            for (int i=0; i < List.Count; ++i)
            {
                if (List[i].Enabled &&
                    !List[i].Sleep)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetSelectedToSleep()
        {
            List[GetSelected()].Sleep = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool AllSlept()
        {
            return GetFirstTidNotSlept() < 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="RuntimeException"></exception>
        public int TryGetSelected()
        {
            int res = -1;
            for (int i = 0; i < List.Count; ++i)
            {
                if (List[i].Selected)
                {
                    if (res != -1)
                    {
                        throw new RuntimeException("DFS Strategy: More than one selected tid entry!");
                    }
                    res = i;
                }
            }
            

            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsNoneSelected()
        {
            return TryGetSelected() < 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="RuntimeException"></exception>
        public int GetSelected()
        {
            int res = TryGetSelected();
            if (res == -1)
            {
                throw new RuntimeException("DFS Strategy: No selected tid entry!");
            }
            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ClearSelected()
        {
            List[GetSelected()].Selected = false;
        }
    }
}