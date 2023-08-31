using System;
using UnityActionAnalysis;
using UnityEngine;

namespace UnityRLEnv
{
    public class Unity2048BlindActionProvider : BlindActionProvider
    {
        protected override bool ShouldIgnoreKeyAction(KeyCode keyCode, bool isDown)
        {
            if (isDown && (keyCode == KeyCode.R || keyCode == KeyCode.Escape))
            {
                return true;
            }
            return false;
        }
    }
}
