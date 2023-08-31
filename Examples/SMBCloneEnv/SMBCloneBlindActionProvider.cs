using System;
using UnityActionAnalysis;
using UnityEngine;

namespace UnityRLEnv
{
    public class SMBCloneBlindActionProvider : BlindActionProvider
    {
        protected override bool ShouldIgnoreKeyAction(KeyCode keyCode, bool isDown)
        {
            if (keyCode == KeyCode.Return && isDown)
            {
                return true;
            }
            return false;
        }
    }
}
