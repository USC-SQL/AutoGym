using System.Collections.Generic;
using System.Linq;

namespace UnityRLEnv
{
    /**
     * Hash functions that return the same value across multiple runs, platforms, etc.
     */
    public static class HashUtil
    {
        public static int CombineHashCodesOrderIndependent(IEnumerable<int> hashCodes)
        {
            List<int> hashCodesSorted = new List<int>(hashCodes);
            hashCodesSorted.Sort();
            return CombineHashCodes(hashCodesSorted);
        }

        public static int CombineHashCodes(IEnumerable<int> hashCodes)
        {
            int result = 0;
            foreach (int hash in hashCodes)
            {
                result = 31 * result + hash;
            }
            return result;
        }

        public static int HashInt(int x)
        {
            return x;
        }

        public static int HashString(string s)
        {
            return CombineHashCodes(s.Select(c => HashInt(c)));
        }
    }
}
