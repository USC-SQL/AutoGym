using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityActionAnalysis
{
    public abstract class InputCondition
    {
        public abstract void PerformInput(InputSimulator sim, InputManagerSettings inputManagerSettings);
    }

    public class AxisInputCondition : InputCondition
    {
        public readonly string axisName;
        public readonly float value;

        public AxisInputCondition(string axisName, float value)
        {
            this.axisName = axisName;
            this.value = value;
        }

        public override string ToString()
        {
            return "Input.GetAxis(\"" + axisName + "\") == " + value;
        }

        public override void PerformInput(InputSimulator sim, InputManagerSettings inputManagerSettings)
        {
            List<KeyCode> keyCodesUp = new List<KeyCode>();
            KeyCode? keyCodeDown = null;

            KeyCode? positiveKey = inputManagerSettings.GetPositiveKey(axisName);
            KeyCode? negativeKey = inputManagerSettings.GetNegativeKey(axisName);

            if (value > 0.0f)
            {
                if (positiveKey.HasValue)
                {
                    keyCodeDown = positiveKey.Value;
                }
                if (negativeKey.HasValue)
                {
                    keyCodesUp.Add(negativeKey.Value);
                }
            } else if (value < 0.0f)
            {
                if (negativeKey.HasValue)
                {
                    keyCodeDown = negativeKey.Value;
                }
                if (positiveKey.HasValue)
                {
                    keyCodesUp.Add(positiveKey.Value);
                }
            } else
            {
                if (positiveKey.HasValue)
                {
                    keyCodesUp.Add(positiveKey.Value);
                }
                if (negativeKey.HasValue)
                {
                    keyCodesUp.Add(negativeKey.Value);
                }
            }
            foreach (var keyCode in keyCodesUp)
            {
                sim.SimulateKeyUp(keyCode);
            }
            if (keyCodeDown != null)
            {
                sim.SimulateKeyDown(keyCodeDown.Value);
            }
        }
    }

    public class ButtonInputCondition : InputCondition
    {
        public readonly string buttonName;
        public readonly bool isDown;

        public ButtonInputCondition(string buttonName, bool isDown)
        {
            this.buttonName = buttonName;
            this.isDown = isDown;
        }

        public override string ToString()
        {
            return "Input.GetButton(\"" + buttonName + "\") == " + isDown;
        }

        public override void PerformInput(InputSimulator sim, InputManagerSettings inputManagerSettings)
        {
            KeyCode? positiveKey = inputManagerSettings.GetPositiveKey(buttonName);
            if (positiveKey.HasValue)
            {
                var keyCode = positiveKey.Value;
                if (isDown)
                {
                    sim.SimulateKeyDown(keyCode);
                } else
                {
                    sim.SimulateKeyUp(keyCode);
                }
            } else
            {
                Debug.LogWarning("failed to perform button input, no positive key found for: " + buttonName);
            }
        }
    }

    public class KeyInputCondition : InputCondition
    {
        public readonly KeyCode keyCode;
        public readonly bool isDown;

        public KeyInputCondition(KeyCode keyCode, bool isDown)
        {
            this.keyCode = keyCode;
            this.isDown = isDown;
        }

        public override string ToString()
        {
            return "Input.GetKey(KeyCode." + keyCode + ") == " + isDown;
        }
        public override void PerformInput(InputSimulator sim, InputManagerSettings inputManagerSettings)
        {
            if (isDown)
            {
                sim.SimulateKeyDown(keyCode);
            }
            else
            {
                sim.SimulateKeyUp(keyCode);
            }
        }
    }

    public class MousePositionInputCondition : InputCondition
    {
        public enum VectorAxis
        {
            X_AXIS = 0,
            Y_AXIS = 1
        }

        public readonly VectorAxis axis;
        public readonly float value;

        public MousePositionInputCondition(VectorAxis axis, float value)
        {
            this.axis = axis;
            this.value = value;
        }

        public override string ToString()
        {
            string axisStr = axis == VectorAxis.X_AXIS ? "x" : "y";
            return "Input.mousePosition." + axisStr + " == " + value;
        }

        public override void PerformInput(InputSimulator sim, InputManagerSettings inputManagerSettings)
        {
            switch (axis)
            {
                case VectorAxis.X_AXIS:
                    sim.SimulateMouseX(value);
                    break;
                case VectorAxis.Y_AXIS:
                    sim.SimulateMouseY(value);
                    break;
                default:
                    throw new Exception("unexpected axis " + axis);
            }
        }
    }
}