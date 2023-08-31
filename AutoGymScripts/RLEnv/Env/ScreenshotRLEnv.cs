using System;
using System.Collections;
using UnityEngine;

namespace UnityRLEnv
{
    public class ScreenshotRLEnv : RLEnv
    {
        protected override void ConfigureEnv()
        {
            SetObservationProvider(new ScreenshotObservationProvider());
            if (envConfig.ContainsKey("blind_actions") && envConfig["blind_actions"].ToObject<bool>())
            {
                SetActionProvider(new BlindActionProvider());
            } else if (envConfig.ContainsKey("symex_actions") && envConfig["symex_actions"].ToObject<bool>())
            {
                SetActionProvider(new SymexActionProvider());
            }
            SetRewardProvider(new NullRewardProvider()); // default reward is 0 always
        }

        protected override bool IsDone()
        {
            return false;
        }
    }
}
