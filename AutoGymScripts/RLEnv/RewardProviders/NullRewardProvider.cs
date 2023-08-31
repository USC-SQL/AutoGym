using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityRLEnv
{
    public class NullRewardProvider : IRewardProvider
    {
        public IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context)
        {
            yield break;
        }

        public float EvaluateAction()
        {
            return 0.0f;
        }
    }
}
