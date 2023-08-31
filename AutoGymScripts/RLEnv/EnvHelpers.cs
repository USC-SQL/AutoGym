using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UnityRLEnv
{
    public class EnvHelpers
    {
        private MonoBehaviour context;

        public EnvHelpers(MonoBehaviour context)
        {
            this.context = context;
        }

        public IEnumerator WaitForGameObject(string gameObjectName)
        {
            while (GameObject.Find(gameObjectName) == null)
            {
                yield return null;
            }
        }

        public IEnumerator PressButton(string gameObjectName)
        {
            yield return context.StartCoroutine(WaitForGameObject(gameObjectName));
            GameObject btn = GameObject.Find(gameObjectName);
            ExecuteEvents.Execute(btn, new PointerEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield break;
        }
    }
}
