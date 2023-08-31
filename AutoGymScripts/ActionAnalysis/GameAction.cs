using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityActionAnalysis
{
    public class GameAction
    {
        public readonly SymexPath path;
        public readonly MonoBehaviour instance;

        public GameAction(SymexPath path, MonoBehaviour instance)
        {
            this.path = path;
            this.instance = instance;
        }

        public bool TrySolve(out InputConditionSet inputConditions)
        {
            if (path.SolveForInputs(instance, out inputConditions))
            {
                return true;
            } else
            {
                inputConditions = null;
                return false;
            }
        }
    }
}
