using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace UnityActionAnalysis
{
    public interface IPreconditionFuncs
    {
        Dictionary<MethodInfo, List<Func<MonoBehaviour, bool>>> Preconditions { get; }
    }
}
