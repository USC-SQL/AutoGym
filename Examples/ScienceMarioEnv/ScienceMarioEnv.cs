using UnityEngine;

namespace UnityRLEnv
{
    public class ScienceMarioEnv : ScreenshotRLEnv
    {
        protected override void ConfigureEnv()
        {
            base.ConfigureEnv();
            if (envConfig.ContainsKey("manual_actions") && envConfig["manual_actions"].ToObject<bool>())
            {
                SetActionProvider(new ScienceMarioManualActionProvider());
            }
        }

        protected override bool IsDone()
        {
            return GameObject.Find("m") == null;
        }
    }
}
