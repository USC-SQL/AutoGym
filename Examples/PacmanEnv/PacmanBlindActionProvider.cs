using System;
using UnityActionAnalysis;
using UnityEngine;

namespace UnityRLEnv
{
    public class PacmanBlindActionProvider : BlindActionProvider
    {
        protected override bool ShouldIgnoreKeyAction(KeyCode keyCode, bool isDown)
        {
            if (keyCode == KeyCode.Escape && isDown)
            {
                return true;
            }
            return false;
        }
    }
}
