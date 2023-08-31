using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityActionAnalysis;
using UnityEngine;

namespace UnityRLEnv
{
    public interface IActionProvider
    {
        IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context);
        int GetActionCount(); 
        ISet<int> GetValidActions();
        bool PerformAction(int actionId, InputSimulator inputSim, MonoBehaviour context);
    }
}
