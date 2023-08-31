using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UnityRLEnv
{
    public class PacmanEnv : ScreenshotRLEnv
    {
        protected override void ConfigureEnv()
        {
            base.ConfigureEnv();
            if (envConfig.ContainsKey("manual_actions") && envConfig["manual_actions"].ToObject<bool>())
            {
                SetActionProvider(new PacmanManualActionProvider());
            }
            if (actionProvider is SymexActionProvider)
            {
                SetActionProvider(new PacmanSymexActionProvider());
            } else if (actionProvider is BlindActionProvider)
            {
                SetActionProvider(new PacmanBlindActionProvider());
            }
        }

        protected override IEnumerator EnterInitialState()
        {
            Button playButton = GameObject.Find("Play").GetComponent<Button>();
            ExecuteEvents.Execute(playButton.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.submitHandler);
            while (GameObject.Find("maze") == null)
            {
                yield return null;
            }
            yield break;
        }

        protected override bool IsDone()
        {
            return GameObject.Find("pacman") == null;
        }
    }
}
