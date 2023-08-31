using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityActionAnalysis;
using UnityEngine;

namespace UnityRLEnv
{
    public abstract class ManualActionProviderBase : IActionProvider
    {
        protected delegate void ManualActionPerformFunc(InputSimulator inputSim);

        struct ManualAction
        {
            public Func<bool> validityCondition;
            public ManualActionPerformFunc performFunc;
        }

        private List<ManualAction> actions;

        public IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context)
        {
            actions = new List<ManualAction>();
            DefineActions();
            yield break;
        }

        public int GetActionCount()
        {
            return actions.Count + 1;
        }

        public ISet<int> GetValidActions()
        {
            HashSet<int> validActions = new HashSet<int>();
            validActions.Add(0);
            for (int actionIndex = 0; actionIndex < actions.Count; ++actionIndex)
            {
                ManualAction action = actions[actionIndex];
                int actionId = actionIndex + 1;
                if (action.validityCondition())
                {
                    validActions.Add(actionId);
                }
            }
            return validActions;
        }

        public bool PerformAction(int actionId, InputSimulator inputSim, MonoBehaviour context)
        {
            if (actionId == 0)
            {
                return false;
            }
            int actionIndex = actionId - 1;
            if (actionIndex >= 0 && actionIndex < actions.Count)
            {
                ManualAction action = actions[actionIndex];
                action.performFunc(inputSim);
                return true;
            } else
            {
                Debug.LogError("ManualActionProviderBase: tried to perform invalid action with id " + actionId);
            }
            return false;
        }

        protected void DefineAction(Func<bool> validityCondition, ManualActionPerformFunc performFunc) 
        {
            actions.Add(new ManualAction { 
                validityCondition = validityCondition, 
                performFunc = performFunc 
            });
        }

        protected delegate void ManualObjectActionPerformFunc<T>(T inst, InputSimulator inputSim);

        protected void DefineObjectAction<T>(Func<T, bool> validityCondition, ManualObjectActionPerformFunc<T> performFunc) where T : MonoBehaviour
        {
            DefineAction(() =>
            {
                foreach (UnityEngine.Object obj in UnityEngine.Object.FindObjectsOfType(typeof(T)))
                {
                    T inst = (T)obj;
                    if (inst.isActiveAndEnabled && inst.gameObject.activeInHierarchy && validityCondition(inst))
                    {
                        return true;
                    }
                }
                return false;
            }, inputSim =>
            {
                T inst;
                List<T> instances = new List<T>();
                foreach (UnityEngine.Object obj in UnityEngine.Object.FindObjectsOfType(typeof(T)))
                {
                    inst = (T)obj;
                    if (inst.isActiveAndEnabled && inst.gameObject.activeInHierarchy && validityCondition(inst))
                    {
                        instances.Add(inst);
                    }
                }
                inst = instances[UnityEngine.Random.Range(0, instances.Count)];
                performFunc(inst, inputSim);
            });
        }


        protected abstract void DefineActions();
    }
}
