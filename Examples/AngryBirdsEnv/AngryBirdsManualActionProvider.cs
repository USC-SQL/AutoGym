using System;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityActionAnalysis;

namespace UnityRLEnv
{
    public class AngryBirdsManualActionProvider : ManualActionProviderBase
    {
        private void DefineAimAction(float mouseRelX, float mouseRelY, FieldInfo isPressed)
        {
            DefineObjectAction<bird>(
                b => (bool)isPressed.GetValue(b),
                (b, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new MousePositionInputCondition(MousePositionInputCondition.VectorAxis.X_AXIS, mouseRelX),
                    new MousePositionInputCondition(MousePositionInputCondition.VectorAxis.Y_AXIS, mouseRelY)
                }));
        }

        protected override void DefineActions()
        {
            FieldInfo isPressed = typeof(bird).GetField("isPressed", BindingFlags.NonPublic | BindingFlags.Instance);

            // Grab bird
            DefineObjectAction<bird>(
                b => !(bool)isPressed.GetValue(b),
                (b, inputSim) =>
                {
                    if (UnityHelpers.ComputeObjectMouseBounds(b.gameObject, out Vector2 pixelMin, out Vector2 pixelMax))
                    {
                        Vector2 center = (pixelMin + pixelMax) / 2.0f;
                        float mouseRelX = center.x / Screen.width;
                        float mouseRelY = center.y / Screen.height;
                        inputSim.PerformAction(new InputConditionSet
                        {
                            new MousePositionInputCondition(MousePositionInputCondition.VectorAxis.X_AXIS, mouseRelX),
                            new MousePositionInputCondition(MousePositionInputCondition.VectorAxis.Y_AXIS, mouseRelY),
                        });
                        inputSim.PerformAction(new InputConditionSet
                        {
                            new KeyInputCondition(KeyCode.Mouse0, true)
                        });
                    }
                });



            // Aim
            for (int i = 0; i < 4; ++i)
            {
                for (int j = 0; j < 4; ++j)
                {
                    float relY = (i + 0.5f) / 4.0f;
                    float relX = (j + 0.5f) / 4.0f;
                    DefineAimAction(relX, relY, isPressed);
                }
            }

            // Release bird
            DefineAction(
                () => true,
                inputSim => inputSim.PerformAction(new InputConditionSet
                {
                    new KeyInputCondition(KeyCode.Mouse0, false)
                }));
        }
    }
}
