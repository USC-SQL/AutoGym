using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityRLEnv
{
    public class StateInfoProvider : IInfoProvider
    {
        private ExplorationStateHasher stateHasher;

        public IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context)
        {
            stateHasher = new ExplorationStateHasher();
            yield break;
        }

        public void AddInfo(JObject info)
        {
            info["state_hash"] = JToken.FromObject(stateHasher.ComputeCurrentHash());
        }
    }
}
