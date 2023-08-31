using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityRLEnv
{
    public abstract class ScoreRewardProvider : IRewardProvider
    {
        private float? lastScore;

        public IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context)
        {
            lastScore = null;
            yield break;
        }

        public float EvaluateAction()
        {
            float currentScore = ReadScore();
            if (!lastScore.HasValue)
            {
                lastScore = currentScore;
                return 0.0f;
            } else
            {
                float rew = currentScore - lastScore.Value;
                lastScore = currentScore;
                return rew;
            }
        }

        protected abstract float ReadScore();
    }
}
