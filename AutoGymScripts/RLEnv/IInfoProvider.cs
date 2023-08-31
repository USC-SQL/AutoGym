using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityRLEnv
{
    public interface IInfoProvider
    {
        IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context);
        void AddInfo(JObject info);
    }
}
