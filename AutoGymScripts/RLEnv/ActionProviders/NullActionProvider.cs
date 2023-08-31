using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityActionAnalysis;
using UnityEngine;

namespace UnityRLEnv
{
    public class NullActionProvider : IActionProvider
    {
        private ISet<int> emptySet = new HashSet<int>();

        public int GetActionCount()
        {
            return 0;
        }

        public ISet<int> GetValidActions()
        {
            return emptySet;
        }

        public IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context)
        {
            yield break;
        }

        public bool PerformAction(int actionId, InputSimulator inputSim, MonoBehaviour context)
        {
            return false;
        }
    }
}
