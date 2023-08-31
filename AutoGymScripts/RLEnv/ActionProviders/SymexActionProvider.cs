using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityActionAnalysis;

namespace UnityRLEnv
{
    public class SymexActionProvider : IActionProvider
    {
        private string symexDatabase;

        protected ActionManager actionManager;
        protected Dictionary<int, List<GameAction>> availableActions;

        public IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context)
        {
            symexDatabase = config["symex_database_path"].ToObject<string>();
            actionManager = new ActionManager();
            foreach (bool status in actionManager.LoadActions(symexDatabase))
            {
                if (!status)
                {
                    // avoid locking up the application when many actions need to be loaded
                    yield return null;
                }
            }
            yield break;
        }

        public int GetActionCount()
        {
            return actionManager.ActionCount + 1;
        }

        public virtual ISet<int> GetValidActions()
        {
            availableActions = actionManager.DetermineValidActions();
            ISet<int> result = new HashSet<int>(availableActions.Keys);
            result.Add(0);
            return result;
        }

        protected virtual bool ShouldIgnoreAction(GameAction action, InputConditionSet inputConds)
        {
            return false;
        }

        public virtual bool PerformAction(int actionId, InputSimulator inputSim, MonoBehaviour context)
        {
            if (actionId == 0)
            {
                return false;
            }
            if (availableActions.TryGetValue(actionId, out List<GameAction> actionInstances))
            {
                GameAction action = actionInstances[UnityEngine.Random.Range(0, actionInstances.Count)];
                if (action.TrySolve(out InputConditionSet inputConds) && !ShouldIgnoreAction(action, inputConds))
                {
                    inputSim.PerformAction(inputConds);
                    return true;
                }
            }
            else
            {
                Debug.LogError("tried to perform unavailable action with id " + actionId);
            }
            return false;
        }
    }
}