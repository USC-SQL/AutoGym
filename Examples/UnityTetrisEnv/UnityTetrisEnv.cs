using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UnityRLEnv
{
    public class UnityTetrisEnv : ScreenshotRLEnv
    {
        protected override void ConfigureEnv()
        {
            base.ConfigureEnv();
            if (envConfig.ContainsKey("manual_actions") && envConfig["manual_actions"].ToObject<bool>())
            {
                SetActionProvider(new UnityTetrisManualActionProvider());
            }
        }

        protected override bool IsDone()
        {
            GameObject gameOverPanel = GameObject.Find("GameOverPanel");
            var gameOverCg = gameOverPanel.GetComponent<CanvasGroup>();
            return gameOverCg.alpha > 0.1f;
        }
    }
}
