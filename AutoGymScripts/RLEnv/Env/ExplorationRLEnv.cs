using System;
using System.Collections;
using UnityEngine;

namespace UnityRLEnv
{
    public class ExplorationRLEnv : ScreenshotRLEnv
    {
        protected override void ConfigureEnv()
        {
            base.ConfigureEnv();
            SetRewardProvider(new ExplorationRewardProvider());
        }
    }
}
