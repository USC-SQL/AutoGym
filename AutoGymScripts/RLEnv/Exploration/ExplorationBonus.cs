using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityRLEnv
{
    public interface ExplorationBonus
    {
        public float CalculateBonus(int visitCount);
    }

    public class InvSqrtExplorationBonus : ExplorationBonus
    {
        private float beta;

        public InvSqrtExplorationBonus(float beta = 1.0f)
        {
            this.beta = beta;
        }

        public float CalculateBonus(int visitCount)
        {
            return beta / Mathf.Sqrt(visitCount);
        }
    }
}
