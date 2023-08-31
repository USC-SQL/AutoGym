using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityRLEnv
{
    public interface IFailureDetector
    {
        IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context);

        void DetectFailures(ISet<string> failuresOut);
    }
}