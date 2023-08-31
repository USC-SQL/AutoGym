using System;
using Microsoft.Z3;

namespace UnityActionAnalysis
{
    public class ResolutionException : Exception
    {
        public string Expression { get; private set; }

        public ResolutionException(string reason, string expr) : base(reason)
        {
            Expression = expr;
        }
    }
}
