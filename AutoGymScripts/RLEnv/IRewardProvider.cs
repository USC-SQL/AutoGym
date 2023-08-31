using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityRLEnv
{
    public interface IRewardProvider
    {
        IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context);
        float EvaluateAction();
    }
}
