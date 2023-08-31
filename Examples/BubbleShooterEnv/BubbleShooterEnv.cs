using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using com.javierquevedo;
using com.javierquevedo.gui;

namespace UnityRLEnv
{
    public class BubbleShooterEnv : ScreenshotRLEnv
    {
        protected override void ConfigureEnv()
        {
            base.ConfigureEnv();
            if (envConfig.ContainsKey("manual_actions") && envConfig["manual_actions"].ToObject<bool>())
            {
                SetActionProvider(new BubbleShooterManualActionProvider());
            }
        }

        protected override IEnumerator EnterInitialState()
        {
            SplashScreenGUI gui;
            while ((gui = (SplashScreenGUI)FindObjectOfType(typeof(SplashScreenGUI))) == null)
            {
                yield return null;
            }

            var sgd = typeof(SplashScreenGUI).GetField("startGameDelegate", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            while (sgd.GetValue(gui) == null)
            {
                yield return null;
            }

            ((SplashScreenGUI.StartGameSelectionDelegate)sgd.GetValue(gui))();
        }

        protected override bool IsDone()
        {
            return GameObject.Find("Camera") != null &&
                GameObject.Find("Camera").GetComponent<GameFinishedGUI>() != null;
        }
    }
}
