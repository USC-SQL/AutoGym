using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityRLEnv
{
    public class ExplorationTracker
    {
        private ExplorationStateHasher stateHasher;
        private Dictionary<int, int> visitCounts;

        public ExplorationTracker(ExplorationStateHasher stateHasher)
        {
            this.stateHasher = stateHasher;
            visitCounts = new Dictionary<int, int>();
        }

        public int GetCurrentStateVisitCount()
        {
            int currentHash = stateHasher.ComputeCurrentHash();
            int newVisitCount;
            if (visitCounts.TryGetValue(currentHash, out int visitCount))
            {
                newVisitCount = visitCount + 1;
            } else
            {
                newVisitCount = 1;
            }
            visitCounts[currentHash] = newVisitCount;
            return newVisitCount;
        }
    }
}
