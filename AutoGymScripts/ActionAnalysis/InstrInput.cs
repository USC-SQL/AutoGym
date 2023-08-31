#if UNITY_EDITOR
#define DEBUG_MOUSE_POSITION
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityActionAnalysis;

namespace UnityActionAnalysis
{
    public class InstrInput
    {
        private static MonoBehaviour simContext; // null if pass-through
        private static InputManagerSettings inputManagerSettings;
        private static ISet<KeyCode> newKeysDown;
        private static ISet<KeyCode> newKeysUp;
        private static ISet<KeyCode> keysHeld;
        private static Dictionary<KeyCode, Coroutine> removeNewCoroutines;
        private static Vector3 mousePos;
        private static ISet<GameObject> prevMouseHitGameObjects;
        private static ISet<GameObject> mouseDownGameObjects;
        private static Coroutine everyFrameHandler;

        private static bool IsPassthrough { get => simContext == null; }

        public static void SetInputManagerSettings(InputManagerSettings inputManagerSettings)
        {
            InstrInput.inputManagerSettings = inputManagerSettings;
        }

        public static void StartSimulation(MonoBehaviour context)
        {
            if (inputManagerSettings == null)
            {
                throw new Exception("set the InputManagerSettings before calling StartSimulation");
            }
            if (simContext != null)
            {
                throw new Exception("simulation already active");
            }
            simContext = context;
            newKeysDown = new HashSet<KeyCode>();
            newKeysUp = new HashSet<KeyCode>();
            keysHeld = new HashSet<KeyCode>();
            removeNewCoroutines = new Dictionary<KeyCode, Coroutine>();
            mousePos = new Vector3(Screen.width/2.0f, Screen.height/2.0f, 0.0f);
            prevMouseHitGameObjects = new HashSet<GameObject>();
            mouseDownGameObjects = new HashSet<GameObject>();
            everyFrameHandler = simContext.StartCoroutine(EveryFrame());
            ResetSimulatedInputs();

#if DEBUG_MOUSE_POSITION
            {
                GameObject debugInputCanvas = new GameObject("InstrInputDebugCanvas");
                UnityEngine.Object.DontDestroyOnLoad(debugInputCanvas);
                Canvas c = debugInputCanvas.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                debugInputCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();
                GameObject debugCursor = new GameObject("InstrInputDebugCursor");
                debugCursor.transform.parent = debugInputCanvas.transform;
                var text = debugCursor.AddComponent<UnityEngine.UI.Text>();
                text.font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
                text.fontSize = 20;
                text.color = Color.red;
                text.text = "X";
                RectTransform cursTransform = (RectTransform)debugCursor.transform;
                cursTransform.sizeDelta = new Vector2(22.0f, 22.0f);
            }
#endif
        }

        public static void StopSimulation()
        {
            if (simContext == null)
            {
                return;
            }
            foreach (Coroutine coro in removeNewCoroutines.Values)
            {
                simContext.StopCoroutine(coro);
            }
            simContext.StopCoroutine(everyFrameHandler);
            everyFrameHandler = null;
            newKeysDown = null;
            newKeysUp = null;
            keysHeld = null;
            removeNewCoroutines = null;
            prevMouseHitGameObjects = null;
            mouseDownGameObjects = null;
            simContext = null;

#if DEBUG_MOUSE_POSITION
            {
                GameObject debugInputCanvas = GameObject.Find("InstrInputDebugCanvas");
                if (debugInputCanvas != null)
                {
                    UnityEngine.Object.Destroy(debugInputCanvas);
                }
            }
#endif
        }

        public static void ResetSimulatedInputs()
        {
            keysHeld.Clear();
            newKeysDown.Clear();
            newKeysUp.Clear();
            foreach (Coroutine coro in removeNewCoroutines.Values)
            {
                simContext.StopCoroutine(coro);
            }
            removeNewCoroutines.Clear();
            prevMouseHitGameObjects.Clear();
            mouseDownGameObjects.Clear();
        }

        private static IEnumerator EveryFrame()
        {
            for (; ; )
            {
                Camera cam = Camera.main;
                if (cam == null)
                {
                    yield return null;
                    continue;
                }

                Vector3 mouseScreenPoint = new Vector3(mousePos.x / Screen.width * cam.pixelWidth, mousePos.y / Screen.height * cam.pixelHeight, 0.0f);
                Ray ray = cam.ScreenPointToRay(mouseScreenPoint);
                RaycastHit[] hits = Physics.RaycastAll(ray);
                RaycastHit2D[] hits2d = Physics2D.RaycastAll(ray.origin, Vector2.zero);
                ISet<GameObject> mouseHitGameObjects = new HashSet<GameObject>();
                IEnumerable<GameObject> candidates = hits.Select(h => h.collider.gameObject).Concat(hits2d.Select(h => h.collider.gameObject));
                foreach (GameObject gameObject in candidates)
                {
                    if (UnityHelpers.ComputeObjectMouseBounds(gameObject, out Vector2 pixelMin, out Vector2 pixelMax))
                    {
                        if (mousePos.x >= pixelMin.x && mousePos.x <= pixelMax.x
                         && mousePos.y >= pixelMin.y && mousePos.y <= pixelMax.y)
                        {
                            mouseHitGameObjects.Add(gameObject);
                        }
                    }
                }

                ISet<GameObject> mouseEnteredGameObjects = new HashSet<GameObject>();
                ISet<GameObject> mouseExitedGameObjects = new HashSet<GameObject>();

                foreach (GameObject gameObject in mouseHitGameObjects)
                {
                    if (!prevMouseHitGameObjects.Contains(gameObject))
                    {
                        mouseEnteredGameObjects.Add(gameObject);
                    }
                }

                foreach (GameObject gameObject in prevMouseHitGameObjects)
                {
                    if (!mouseHitGameObjects.Contains(gameObject))
                    {
                        mouseExitedGameObjects.Add(gameObject);
                    }
                }

                foreach (GameObject gameObject in mouseEnteredGameObjects)
                {
                    if (gameObject == null)
                    {
                        continue;
                    }
                    foreach (MonoBehaviour component in gameObject.GetComponents<MonoBehaviour>())
                    {
                        if (component == null)
                        {
                            continue;
                        }
                        Type componentType = component.GetType();
                        foreach (MethodInfo m in componentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (m.GetParameters().Length == 0 && m.Name == "OnMouseEnter")
                            {
                                m.Invoke(component, new object[0]);
                            }
                        }
                    }
                }

                foreach (GameObject gameObject in mouseExitedGameObjects)
                {
                    if (gameObject == null)
                    {
                        continue;
                    }
                    foreach (MonoBehaviour component in gameObject.GetComponents<MonoBehaviour>())
                    {
                        if (component == null)
                        {
                            continue;
                        }
                        Type componentType = component.GetType();
                        foreach (MethodInfo m in componentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (m.GetParameters().Length == 0 && m.Name == "OnMouseExit")
                            {
                                m.Invoke(component, new object[0]);
                            }
                        }
                    }
                }

                bool mouseDown = newKeysDown.Contains(KeyCode.Mouse0);
                bool mouseUp = newKeysUp.Contains(KeyCode.Mouse0);

                foreach (GameObject gameObject in mouseDownGameObjects)
                {
                    if (gameObject == null)
                    {
                        continue;
                    }
                    foreach (MonoBehaviour component in gameObject.GetComponents<MonoBehaviour>())
                    {
                        if (component == null)
                        {
                            continue;
                        }
                        Type componentType = component.GetType();
                        foreach (MethodInfo m in componentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (m.GetParameters().Length == 0)
                            {
                                switch (m.Name)
                                {
                                    case "OnMouseUp":
                                        if (mouseUp)
                                        {
                                            m.Invoke(component, new object[0]);
                                        }
                                        break;
                                    case "OnMouseUpAsButton":
                                        if (mouseUp && mouseHitGameObjects.Contains(gameObject))
                                        {
                                            m.Invoke(component, new object[0]);
                                        }
                                        break;
                                    case "OnMouseDrag":
                                        if (!mouseUp)
                                        {
                                            m.Invoke(component, new object[0]);
                                        }
                                        break;
                                }
                            }
                        }
                    }
                }

                foreach (GameObject gameObject in mouseHitGameObjects)
                {
                    if (gameObject == null)
                    {
                        continue;
                    }
                    foreach (MonoBehaviour component in gameObject.GetComponents<MonoBehaviour>())
                    {
                        if (component == null)
                        {
                            continue;
                        }
                        Type componentType = component.GetType();
                        foreach (MethodInfo m in componentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (m.GetParameters().Length == 0)
                            {
                                switch (m.Name)
                                {
                                    case "OnMouseOver":
                                        m.Invoke(component, new object[0]);
                                        break;
                                    case "OnMouseDown":
                                        if (mouseDown && !mouseDownGameObjects.Contains(gameObject))
                                        {
                                            m.Invoke(component, new object[0]);
                                        }
                                        break;
                                }
                            }
                        }
                    }
                }

                if (mouseUp)
                {
                    mouseDownGameObjects.Clear();
                }
                if (mouseDown)
                {
                    foreach (GameObject gameObject in mouseHitGameObjects)
                    {
                        mouseDownGameObjects.Add(gameObject);
                    }
                }
                prevMouseHitGameObjects = mouseHitGameObjects;

#if DEBUG_MOUSE_POSITION
                {
                    GameObject debugCursor = GameObject.Find("InstrInputDebugCursor");
                    if (debugCursor != null)
                    {
                        float mx = mousePos.x;
                        float my = mousePos.y;
                        float offsx = 6.0f;
                        float offsy = -2.0f;
                        GameObject debugCanvas = GameObject.Find("InstrInputDebugCanvas");
                        RectTransform canvTransform = (RectTransform)debugCanvas.transform;
                        Vector2 canvSize = canvTransform.sizeDelta;
                        Vector3 canvPos = canvTransform.position;
                        RectTransform cursTransform = (RectTransform)debugCursor.transform;
                        cursTransform.position = new Vector3(
                            (mx/Screen.width-0.5f)*canvSize.x + canvPos.x + offsx, 
                            (my/Screen.height-0.5f)*canvSize.y + canvPos.y + offsy, 0.0f);
                    }
                }
#endif

                yield return null;
            }
        }

        private static IEnumerator RemoveNew(KeyCode keyCode)
        {
            yield return null;
            yield return null;
            newKeysDown.Remove(keyCode);
            newKeysUp.Remove(keyCode);
        }

        private static void ClearKeyState(KeyCode keyCode)
        {
            keysHeld.Remove(keyCode);
            newKeysDown.Remove(keyCode);
            newKeysUp.Remove(keyCode);
            if (removeNewCoroutines.ContainsKey(keyCode))
            {
                simContext.StopCoroutine(removeNewCoroutines[keyCode]);
                removeNewCoroutines.Remove(keyCode);
            }
        }

        private static void ScheduleRemoveNew(KeyCode keyCode)
        {
            Coroutine coro = simContext.StartCoroutine(RemoveNew(keyCode));
            removeNewCoroutines[keyCode] = coro;
        }

        public static void SimulateKeyDown(KeyCode keyCode)
        {
            bool wasHeld = keysHeld.Contains(keyCode);
            ClearKeyState(keyCode);
            keysHeld.Add(keyCode);
            if (!wasHeld)
            {
                newKeysDown.Add(keyCode);
                ScheduleRemoveNew(keyCode);
            }

            if (keyCode == InputManagerSettings.KEYCODE_MOUSEX_NEG)
            {
                mousePos.x = Screen.width / 4.0f;
            } else if (keyCode == InputManagerSettings.KEYCODE_MOUSEX_POS)
            {
                mousePos.x = 3.0f * Screen.width / 4.0f;
            } else if (keyCode == InputManagerSettings.KEYCODE_MOUSEY_NEG)
            {
                mousePos.y = Screen.height / 4.0f;
            } else if (keyCode == InputManagerSettings.KEYCODE_MOUSEY_POS)
            {
                mousePos.y = 3.0f * Screen.height / 4.0f;
            }
        }

        public static void SimulateKeyUp(KeyCode keyCode)
        {
            bool wasHeld = keysHeld.Contains(keyCode);
            ClearKeyState(keyCode);
            if (wasHeld)
            {
                newKeysUp.Add(keyCode);
                ScheduleRemoveNew(keyCode);
            }
        }

        public static void SimulateMouseX(float relX)
        {
            mousePos.x = relX * Screen.width;
        }

        public static void SimulateMouseY(float relY)
        {
            mousePos.y = relY * Screen.height;
        }

        public static Vector3 mousePosition
        {
            get
            {
                if (IsPassthrough)
                {
                    return Input.mousePosition;
                } else
                {
                    return mousePos;
                }
            }
        }

        public static bool GetKey(string name)
        {
            if (IsPassthrough)
            {
                return Input.GetKey(name);
            }
            else
            {
                KeyCode? keyCode = InputManagerSettings.KeyNameToCode(name);
                if (keyCode.HasValue)
                {
                    return keysHeld.Contains(keyCode.Value);
                }
                else
                {
                    Debug.LogWarning("unrecognized key name '" + name + "'");
                    return false;
                }
            }
        }

        public static bool GetKey(KeyCode key)
        {
            if (IsPassthrough)
            {
                return Input.GetKey(key);
            }
            else
            {
                return keysHeld.Contains(key);
            }
        }

        public static bool GetKeyDown(string name)
        {
            if (IsPassthrough)
            {
                return Input.GetKeyDown(name);
            }
            else
            {
                KeyCode? keyCode = InputManagerSettings.KeyNameToCode(name);
                if (keyCode.HasValue)
                {
                    return newKeysDown.Contains(keyCode.Value);
                }
                else
                {
                    Debug.LogWarning("unrecognized key name '" + name + "'");
                    return false;
                }
            }
        }

        public static bool GetKeyDown(KeyCode key)
        {
            if (IsPassthrough)
            {
                return Input.GetKeyDown(key);
            }
            else
            {
                return newKeysDown.Contains(key);
            }
        }

        public static bool GetKeyUp(string name)
        {
            if (IsPassthrough)
            {
                return Input.GetKeyUp(name);
            }
            else
            {
                KeyCode? keyCode = InputManagerSettings.KeyNameToCode(name);
                if (keyCode.HasValue)
                {
                    return newKeysUp.Contains(keyCode.Value);
                }
                else
                {
                    Debug.LogWarning("unrecognized key name '" + name + "'");
                    return false;
                }
            }
        }

        public static bool GetKeyUp(KeyCode key)
        {
            if (IsPassthrough)
            {
                return Input.GetKeyUp(key);
            }
            else
            {
                return newKeysUp.Contains(key);
            }
        }

        public static bool GetButton(string buttonName)
        {
            if (IsPassthrough)
            {
                return Input.GetButton(buttonName);
            }
            else
            {
                KeyCode? keyCode = inputManagerSettings.GetPositiveKey(buttonName);
                if (keyCode.HasValue)
                {
                    return keysHeld.Contains(keyCode.Value);
                }
                else
                {
                    Debug.LogWarning("unrecognized button: '" + buttonName + "'");
                    return false;
                }
            }
        }

        public static bool GetButtonDown(string buttonName)
        {
            if (IsPassthrough)
            {
                return Input.GetButtonDown(buttonName);
            }
            else
            {
                KeyCode? keyCode = inputManagerSettings.GetPositiveKey(buttonName);
                if (keyCode.HasValue)
                {
                    return newKeysDown.Contains(keyCode.Value);
                }
                else
                {
                    Debug.LogWarning("unrecognized button: '" + buttonName + "'");
                    return false;
                }
            }
        }

        public static bool GetButtonUp(string buttonName)
        {
            if (IsPassthrough)
            {
                return Input.GetButtonUp(buttonName);
            }
            else
            {
                KeyCode? keyCode = inputManagerSettings.GetPositiveKey(buttonName);
                if (keyCode.HasValue)
                {
                    return newKeysUp.Contains(keyCode.Value);
                }
                else
                {
                    Debug.LogWarning("unrecognized button: '" + buttonName + "'");
                    return false;
                }
            }
        }

        public static float GetAxis(string axisName)
        {
            if (IsPassthrough)
            {
                return Input.GetAxis(axisName);
            }
            else 
            {
                KeyCode? posKeyCode = inputManagerSettings.GetPositiveKey(axisName);
                KeyCode? negKeyCode = inputManagerSettings.GetNegativeKey(axisName);
                float value = 0.0f;
                if (posKeyCode.HasValue)
                {
                    if (posKeyCode.Value == InputManagerSettings.KEYCODE_MOUSEX_POS)
                    {
                        value = 2.0f * mousePos.x / Screen.width - 1.0f;
                    } else if (posKeyCode.Value == InputManagerSettings.KEYCODE_MOUSEY_POS)
                    {
                        value = 2.0f * mousePos.y / Screen.height - 1.0f;
                    } else if (keysHeld.Contains(posKeyCode.Value))
                    {
                        value += 1.0f;
                    }
                }
                if (negKeyCode.HasValue)
                {
                    if (negKeyCode.Value == InputManagerSettings.KEYCODE_MOUSEX_NEG)
                    {
                        value = 2.0f * mousePos.x / Screen.width - 1.0f;
                    } else if (negKeyCode.Value == InputManagerSettings.KEYCODE_MOUSEY_NEG)
                    {
                        value = 2.0f * mousePos.y / Screen.height - 1.0f;
                    } else if (keysHeld.Contains(negKeyCode.Value))
                    {
                        value -= 1.0f;
                    }
                }
                return value;
            }
        }

        public static float GetAxisRaw(string axisName)
        {
            if (IsPassthrough)
            {
                return Input.GetAxisRaw(axisName);
            }
            else
            {
                return GetAxis(axisName);
            }
        }


        private static KeyCode _MouseButtonToKeyCode(int button)
        {
            if (button < 0 || button > 6)
            {
                throw new ArgumentOutOfRangeException("mouse button " + button + " out of range");
            }
            return KeyCode.Mouse0 + button;
        }


        public static bool GetMouseButton(int button)
        {
            if (IsPassthrough)
            {
                return Input.GetMouseButton(button);
            } else
            {
                return GetKey(_MouseButtonToKeyCode(button));
            }
        }

        public static bool GetMouseButtonDown(int button)
        {
            if (IsPassthrough)
            {
                return Input.GetMouseButtonDown(button);
            } else
            {
                return GetKeyDown(_MouseButtonToKeyCode(button));
            }
        }

        public static bool GetMouseButtonUp(int button)
        {
            if (IsPassthrough)
            {
                return Input.GetMouseButtonUp(button);
            } else
            {
                return GetKeyUp(_MouseButtonToKeyCode(button));
            }
        }



        public static class ExprSpecialVariables
        {

            public static object InstanceMouseBoundsMinX(ExprContext context)
            {
                if (UnityHelpers.ComputeObjectMouseBounds(context.instance.gameObject, out Vector2 pixelMin, out Vector2 pixelMax))
                {
                    return pixelMin.x / Screen.width;
                }
                else
                {
                    return float.NaN;
                }
            }

            public static object InstanceMouseBoundsMaxX(ExprContext context)
            {
                if (UnityHelpers.ComputeObjectMouseBounds(context.instance.gameObject, out Vector2 pixelMin, out Vector2 pixelMax))
                {
                    return pixelMax.x / Screen.width;
                }
                else
                {
                    return float.NaN;
                }
            }

            public static object InstanceMouseBoundsMinY(ExprContext context)
            {
                if (UnityHelpers.ComputeObjectMouseBounds(context.instance.gameObject, out Vector2 pixelMin, out Vector2 pixelMax))
                {
                    return pixelMin.y / Screen.height;
                }
                else
                {
                    return float.NaN;
                }
            }

            public static object InstanceMouseBoundsMaxY(ExprContext context)
            {
                if (UnityHelpers.ComputeObjectMouseBounds(context.instance.gameObject, out Vector2 pixelMin, out Vector2 pixelMax))
                {
                    return pixelMax.y / Screen.height;
                }
                else
                {
                    return float.NaN;
                }
            }

            public static object InstanceMouseDidEnter(ExprContext context)
            {
                return prevMouseHitGameObjects.Contains(context.instance.gameObject);
            }

            public static object InstanceMouseWasDown(ExprContext context)
            {
                return mouseDownGameObjects.Contains(context.instance.gameObject);
            }
        }
    }
}