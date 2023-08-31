using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityActionAnalysis;
using UnityEngine;

namespace UnityRLEnv
{
    public class BlindActionProvider : IActionProvider
    {
        private IList<KeyCode> keyCodes;
        private IList<int> mouseButtons;
        private int mouseGridWidth;
        private int mouseGridHeight;

        private int keyStart;
        private int mouseButtonStart;
        private int mouseMoveStart;

        private List<bool> actionMask;

        public BlindActionProvider()
        {
            keyCodes = new List<KeyCode>
            {
                KeyCode.Backspace,
                KeyCode.Return,
                KeyCode.Pause,
                KeyCode.Escape,
                KeyCode.Space,
                KeyCode.Comma,
                KeyCode.Minus,
                KeyCode.Period,
                KeyCode.LeftControl,
                KeyCode.LeftAlt,
                KeyCode.LeftShift,
                KeyCode.Alpha0,
                KeyCode.Alpha1,
                KeyCode.Alpha2,
                KeyCode.Alpha3,
                KeyCode.Alpha4,
                KeyCode.Alpha5,
                KeyCode.Alpha6,
                KeyCode.Alpha7,
                KeyCode.Alpha8,
                KeyCode.Alpha9,
                KeyCode.A,
                KeyCode.B,
                KeyCode.C,
                KeyCode.D,
                KeyCode.E,
                KeyCode.F,
                KeyCode.G,
                KeyCode.H,
                KeyCode.I,
                KeyCode.J,
                KeyCode.K,
                KeyCode.L,
                KeyCode.M,
                KeyCode.N,
                KeyCode.O,
                KeyCode.P,
                KeyCode.Q,
                KeyCode.R,
                KeyCode.S,
                KeyCode.T,
                KeyCode.U,
                KeyCode.V,
                KeyCode.W,
                KeyCode.X,
                KeyCode.Y,
                KeyCode.Z,
                KeyCode.Delete,
                KeyCode.UpArrow,
                KeyCode.DownArrow,
                KeyCode.RightArrow,
                KeyCode.LeftArrow,
                KeyCode.Home,
                KeyCode.End,
                KeyCode.F1,
                KeyCode.F2,
                KeyCode.F3,
                KeyCode.F4,
                KeyCode.F5,
                KeyCode.F6,
                KeyCode.F7,
                KeyCode.F8,
                KeyCode.F9,
                KeyCode.F10,
                KeyCode.F11,
                KeyCode.F12
            };

            mouseButtons = new List<int> { 0, 1, 2 };
            mouseGridWidth = 4;
            mouseGridHeight = 4;

            actionMask = new List<bool>();

            actionMask.Add(false); // do nothing
            keyStart = actionMask.Count;
            for (int i = 0; i < keyCodes.Count; ++i)
            {
                actionMask.Add(false); // key down
                actionMask.Add(false); // key up
            }
            mouseButtonStart = actionMask.Count;
            for (int i = 0; i < mouseButtons.Count; ++i)
            {
                actionMask.Add(false); // mouse button down
                actionMask.Add(false); // mouse button up
            }
            mouseMoveStart = actionMask.Count;
            for (int i = 0; i < mouseGridHeight; ++i)
            {
                for (int j = 0; j < mouseGridWidth; ++j)
                {
                    actionMask.Add(true); // move mouse position
                }
            }
        }

        public IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context)
        {
            yield break;
        }

        public int GetActionCount()
        {
            return actionMask.Count;
        }

        public ISet<int> GetValidActions()
        {
            for (int i = 0; i < keyCodes.Count; ++i)
            {
                KeyCode keyCode = keyCodes[i];
                bool isKeyHeld = InstrInput.GetKey(keyCode);
                actionMask[keyStart+2*i] = !isKeyHeld;
                actionMask[keyStart+2*i+1] = isKeyHeld;
            }
            for (int i = 0; i < mouseButtons.Count; ++i)
            {
                int mouseButton = mouseButtons[i];
                bool isMbDown = InstrInput.GetMouseButton(mouseButton);
                actionMask[mouseButtonStart+2*i] = !isMbDown;
                actionMask[mouseButtonStart+2*i+1] = isMbDown;
            }
            ISet<int> validActions = new HashSet<int>();
            for (int i = 0; i < actionMask.Count; ++i)
            {
                if (actionMask[i])
                {
                    validActions.Add(i);
                }
            }
            return validActions;
        }

        protected virtual bool ShouldIgnoreKeyAction(KeyCode keyCode, bool isDown)
        {
            return false;
        }

        protected virtual bool ShouldIgnoreMouseMovementAction(float mx, float my)
        {
            return false;
        }

        public bool PerformAction(int actionId, InputSimulator inputSim, MonoBehaviour context)
        {
            if (actionId == 0)
            {
                return false; // do nothing
            } else if (actionId >= keyStart && actionId < mouseButtonStart)
            {
                int keyCodeIndex = (actionId - keyStart) / 2;
                bool isDown = (actionId - keyStart) % 2 == 0;
                KeyCode keyCode = keyCodes[keyCodeIndex];
                if (!ShouldIgnoreKeyAction(keyCode, isDown))
                {
                    inputSim.PerformAction(new InputConditionSet { new KeyInputCondition(keyCode, isDown) });
                    return true;
                }
            } else if (actionId >= mouseButtonStart && actionId < mouseMoveStart)
            {
                int mouseButtonIndex = (actionId - mouseButtonStart) / 2;
                bool isDown = (actionId - mouseButtonStart) % 2 == 0;
                int mouseButton = mouseButtons[mouseButtonIndex];
                KeyCode mouseButtonKeyCode = KeyCode.Mouse0 + mouseButton;
                if (!ShouldIgnoreKeyAction(mouseButtonKeyCode, isDown))
                {
                    inputSim.PerformAction(new InputConditionSet { new KeyInputCondition(mouseButtonKeyCode, isDown) });
                    return true;
                }
            } else if (actionId >= mouseMoveStart && actionId < actionMask.Count)
            {
                int row = (actionId - mouseMoveStart) / mouseGridWidth;
                int col = actionId % mouseGridWidth;
                float my = ((float)row) / mouseGridHeight;
                float mx = ((float)col) / mouseGridWidth;
                mx += UnityEngine.Random.Range(0.0f, 1.0f / mouseGridWidth);
                my += UnityEngine.Random.Range(0.0f, 1.0f / mouseGridHeight);
                if (!ShouldIgnoreMouseMovementAction(mx, my))
                {
                    inputSim.PerformAction(new InputConditionSet {
                        new MousePositionInputCondition(MousePositionInputCondition.VectorAxis.X_AXIS, mx),
                        new MousePositionInputCondition(MousePositionInputCondition.VectorAxis.Y_AXIS, my)
                    });
                    return true;
                }
            } else
            {
                Debug.LogError("BlindActionProvider: tried to perform invalid action with id " + actionId);
            }
            return false;
        }
    }
}
