using System;
using UnityActionAnalysis;
using UnityEngine;

namespace UnityRLEnv
{
    public class Unity2048SymexActionProvider : SymexActionProvider
    {
        protected override bool ShouldIgnoreAction(GameAction action, InputConditionSet inputConds)
        {
            foreach (InputCondition inputCond in inputConds)
            {
                if (inputCond is ButtonInputCondition buttonCond && buttonCond.isDown
                    && (buttonCond.buttonName == "Reset" || buttonCond.buttonName == "Quit"))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
