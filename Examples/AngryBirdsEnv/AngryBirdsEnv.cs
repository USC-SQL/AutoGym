using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UnityRLEnv
{
    public class AngryBirdsEnv : ScreenshotRLEnv
    {
        protected override void ConfigureEnv()
        {
            base.ConfigureEnv();
            if (envConfig.ContainsKey("manual_actions") && envConfig["manual_actions"].ToObject<bool>())
            {
                SetActionProvider(new AngryBirdsManualActionProvider());
            }
        }
    }
}
