using System;
using UnityActionAnalysis;
using UnityEngine;

namespace UnityRLEnv
{
    public class SMBCloneSymexActionProvider : SymexActionProvider
    {
        protected override bool ShouldIgnoreAction(GameAction action, InputConditionSet inputConds)
        {
            foreach (InputCondition inputCond in inputConds)
            {
                if (inputCond is ButtonInputCondition buttonCond && buttonCond.isDown && buttonCond.buttonName == "Pause")
                {
                    return true;
                }
            }
            return false;
        }
    }
}
