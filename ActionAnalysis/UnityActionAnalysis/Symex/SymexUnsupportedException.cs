using System;

namespace UnityActionAnalysis
{
    public class SymexUnsupportedException : Exception
    {
        public SymexUnsupportedException(string reason) : base(reason)
        {
        }
    }
}
