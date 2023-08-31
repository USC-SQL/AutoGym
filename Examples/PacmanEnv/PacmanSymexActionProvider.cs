using System;
using UnityActionAnalysis;
using UnityEngine;

namespace UnityRLEnv
{
    public class PacmanSymexActionProvider : SymexActionProvider
    {
        protected override bool ShouldIgnoreAction(GameAction action, InputConditionSet inputConds)
        {
            foreach (InputCondition inputCond in inputConds)
            {
                if (inputCond is KeyInputCondition keyCond && keyCond.isDown && keyCond.keyCode == KeyCode.Escape)
                {
                    return true;
                }
            }
            return false;
        }
    }
}