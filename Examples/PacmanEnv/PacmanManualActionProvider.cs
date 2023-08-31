using System;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityActionAnalysis;

namespace UnityRLEnv
{
    public class PacmanManualActionProvider : ManualActionProviderBase
    {
        protected override void DefineActions()
        {
            // Move right
            DefineObjectAction<PlayerController>(
                pc => true,
                (pc, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new AxisInputCondition("Horizontal", 1.0f)
                }));

            // No horizontal movement
            DefineObjectAction<PlayerController>(
                pc => true,
                (pc, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new AxisInputCondition("Horizontal", 0.0f)
                }));

            // Move left
            DefineObjectAction<PlayerController>(
                pc => true,
                (pc, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new AxisInputCondition("Horizontal", -1.0f)
                }));

            // Move up
            DefineObjectAction<PlayerController>(
                pc => true,
                (pc, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new AxisInputCondition("Vertical", 1.0f)
                }));

            // No vertical movement
            DefineObjectAction<PlayerController>(
                pc => true,
                (pc, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new AxisInputCondition("Vertical", 0.0f)
                }));

            // Move down
            DefineObjectAction<PlayerController>(
                pc => true,
                (pc, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new AxisInputCondition("Vertical", -1.0f)
                }));
        }
    }
}
