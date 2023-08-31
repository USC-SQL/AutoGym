using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityRLEnv
{
    public interface IObservationProvider
    {
        IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context);
        IList<float> CollectObservations();
        string CollectImageObservation();
    }
}
