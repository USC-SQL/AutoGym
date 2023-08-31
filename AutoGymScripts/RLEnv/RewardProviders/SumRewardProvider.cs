using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityRLEnv
{
    public class SumRewardProvider : IRewardProvider
    {
        private IRewardProvider rewardProviderA;
        private IRewardProvider rewardProviderB;

        public SumRewardProvider(IRewardProvider rewardProviderA, IRewardProvider rewardProviderB)
        {
            this.rewardProviderA = rewardProviderA;
            this.rewardProviderB = rewardProviderB;
        }

        public IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context)
        {
            yield return context.StartCoroutine(rewardProviderA.Initialize(envId, workDir, config, context));
            yield return context.StartCoroutine(rewardProviderB.Initialize(envId, workDir, config, context));
        }

        public float EvaluateAction()
        {
            float rewardA = rewardProviderA.EvaluateAction();
            float rewardB = rewardProviderB.EvaluateAction();
            return rewardA + rewardB;
        }
    }
}
