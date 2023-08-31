using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UnityRLEnv
{
    public class Unity2048Env : ScreenshotRLEnv
    {
        protected override void ConfigureEnv()
        {
            base.ConfigureEnv();
            if (envConfig.ContainsKey("manual_actions") && envConfig["manual_actions"].ToObject<bool>())
            {
                SetActionProvider(new Unity2048ManualActionProvider());
            }
            if (actionProvider is SymexActionProvider)
            {
                SetActionProvider(new Unity2048SymexActionProvider());
            } else if (actionProvider is BlindActionProvider)
            {
                SetActionProvider(new Unity2048BlindActionProvider());
            }
        }

        protected override bool IsDone()
        {
            GameObject gameOverPanel = GameObject.Find("GameOver Panel");
            return gameOverPanel != null && gameOverPanel.activeInHierarchy;
        }
    }
}
