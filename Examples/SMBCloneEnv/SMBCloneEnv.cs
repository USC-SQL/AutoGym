using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UnityRLEnv 
{
    public class SMBCloneEnv : ScreenshotRLEnv
    {
        protected override void ConfigureEnv()
        {
            base.ConfigureEnv();
            if (envConfig.ContainsKey("manual_actions") && envConfig["manual_actions"].ToObject<bool>())
            {
                SetActionProvider(new SMBCloneManualActionProvider());
            }
            if (actionProvider is SymexActionProvider)
            {
                SetActionProvider(new SMBCloneSymexActionProvider());
            } else if (actionProvider is BlindActionProvider)
            {
                SetActionProvider(new SMBCloneBlindActionProvider());
            }
        }

        protected override IEnumerator EnterInitialState()
        {
            while (GameObject.Find("Button New Game") == null)
            {
                yield return null;
            }

            Button newGameButton = GameObject.Find("Button New Game").GetComponent<Button>();
            ExecuteEvents.Execute(newGameButton.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.submitHandler);
            while (GameObject.Find("Mario") == null)
            {
                yield return null;
            }

            yield break;
        }

        protected override bool IsDone()
        {
            return GameObject.Find("GAME OVER") != null;
        }
    }
}
