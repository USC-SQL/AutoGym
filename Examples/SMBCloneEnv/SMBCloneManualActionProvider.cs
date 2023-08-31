using System;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityActionAnalysis;

namespace UnityRLEnv
{
    public class SMBCloneManualActionProvider : ManualActionProviderBase
    {
        protected override void DefineActions()
        {
            // Move left
            DefineObjectAction<Mario>(
                m => !m.inputFreezed,
                (m, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new AxisInputCondition("Horizontal", -1.0f)
                }));

            // Stay still
            DefineObjectAction<Mario>(
                m => !m.inputFreezed,
                (m, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new AxisInputCondition("Horizontal", 0.0f)
                }));

            // Move right
            DefineObjectAction<Mario>(
                m => !m.inputFreezed,
                (m, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new AxisInputCondition("Horizontal", 1.0f)
                }));

            // Run/Shoot
            DefineObjectAction<Mario>(
                m => !m.inputFreezed,
                (m, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new ButtonInputCondition("Dash", true)
                }));

            // Crouch
            DefineObjectAction<Mario>(
                m => !m.inputFreezed,
                (m, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new ButtonInputCondition("Crouch", true)
                }));

            // Jump
            DefineObjectAction<Mario>(
                m => !m.inputFreezed,
                (m, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new ButtonInputCondition("Jump", true)
                }));

            // Stop moving
            DefineAction(
                () => true,
                inputSim => inputSim.PerformAction(new InputConditionSet
                {
                    new ButtonInputCondition("Dash", false),
                    new ButtonInputCondition("Crouch", false),
                    new ButtonInputCondition("Jump", false)
                }));
        }
    }
}
