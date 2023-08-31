using System;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityActionAnalysis;

namespace UnityRLEnv
{
    public class FruitNinjaManualActionProvider : ManualActionProviderBase
    {
        private void DefineMoveBladeAction(float mouseRelX, float mouseRelY, FieldInfo isCutting)
        {
            DefineObjectAction<Blade>(
                b => (bool)isCutting.GetValue(b),
                (b, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new MousePositionInputCondition(MousePositionInputCondition.VectorAxis.X_AXIS, mouseRelX),
                    new MousePositionInputCondition(MousePositionInputCondition.VectorAxis.Y_AXIS, mouseRelY),
                }));
        }

        protected override void DefineActions()
        {
            FieldInfo isCutting = typeof(Blade).GetField("isCutting", BindingFlags.NonPublic | BindingFlags.Instance);

            // Start cutting
            DefineObjectAction<Blade>(
                b => !InstrInput.GetMouseButton(0),
                (b, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new KeyInputCondition(KeyCode.Mouse0, true)
                }));

            // Stop cutting
            DefineObjectAction<Blade>(
                b => InstrInput.GetMouseButton(0),
                (b, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new KeyInputCondition(KeyCode.Mouse0, false)
                }));

            // Move blade (4x4 grid)
            for (int i = 0; i < 4; ++i)
            {
                for (int j = 0; j < 4; ++j)
                {
                    float relY = (i + 0.5f) / 4.0f;
                    float relX = (j + 0.5f) / 4.0f;
                    DefineMoveBladeAction(relX, relY, isCutting);
                }
            }
        }
    }
}
