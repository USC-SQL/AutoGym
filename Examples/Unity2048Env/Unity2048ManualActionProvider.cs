using System;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityActionAnalysis;

namespace UnityRLEnv
{
    public class Unity2048ManualActionProvider : ManualActionProviderBase
    {
        protected override void DefineActions()
        {
            FieldInfo gmState = typeof(GridManager).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);

            // Move left
            DefineObjectAction<GridManager>(
                gm => (int)gmState.GetValue(gm) == 1 && !InstrInput.GetButton("Left"),
                (gm, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new ButtonInputCondition("Left", true)
                }));

            // Move right
            DefineObjectAction<GridManager>(
                gm => (int)gmState.GetValue(gm) == 1 && !InstrInput.GetButton("Right"),
                (gm, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new ButtonInputCondition("Right", true)
                }));

            // Move up
            DefineObjectAction<GridManager>(
                gm => (int)gmState.GetValue(gm) == 1 && !InstrInput.GetButton("Up"),
                (gm, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new ButtonInputCondition("Up", true)
                }));

            // Move down
            DefineObjectAction<GridManager>(
                gm => (int)gmState.GetValue(gm) == 1 && !InstrInput.GetButton("Down"),
                (gm, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new ButtonInputCondition("Down", true)
                }));

            // Stop movement
            DefineAction(
                () => true,
                inputSim => inputSim.PerformAction(new InputConditionSet
                {
                    new ButtonInputCondition("Left", false),
                    new ButtonInputCondition("Right", false),
                    new ButtonInputCondition("Up", false),
                    new ButtonInputCondition("Down", false)
                }));
        }
    }
}
