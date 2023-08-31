using System;
using System.IO;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityRLEnv
{
    public class ExplorationRewardProvider : IRewardProvider
    {
        private ExplorationTracker tracker;
        private ExplorationBonus bonus;

        public IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context)
        {
            string stateDumpDir = null;
            if (config.ContainsKey("dump_states") && config["dump_states"].ToObject<bool>())
            {
                stateDumpDir = Path.Combine(workDir, "StateDumps", DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString());
                if (!Directory.Exists(stateDumpDir))
                {
                    Directory.CreateDirectory(stateDumpDir);
                }
            }
            tracker = new ExplorationTracker(new ExplorationStateHasher(stateDumpDir));
            bonus = new InvSqrtExplorationBonus();
            yield break;
        }

        public float EvaluateAction()
        {
            int visitCount = tracker.GetCurrentStateVisitCount();
            return bonus.CalculateBonus(visitCount);
        }
    }
}
