using System;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityActionAnalysis;

namespace UnityRLEnv
{
    public class ScienceMarioManualActionProvider : ManualActionProviderBase
    {
        protected override void DefineActions()
        {
            FieldInfo lockController = typeof(SMBPlayer).GetField("_lockController", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo isOnGround = typeof(SMBCharacter).GetField("_isOnGround", BindingFlags.NonPublic | BindingFlags.Instance);

            // Start running
            DefineObjectAction<SMBPlayer>(
                p => !(bool)lockController.GetValue(p),
                (p, inputSim) => inputSim.PerformAction(new InputConditionSet {
                    new KeyInputCondition(KeyCode.Z, true)
                }));

            // Move left
            DefineObjectAction<SMBPlayer>(
                p => !(bool)lockController.GetValue(p),
                (p, inputSim) => inputSim.PerformAction(new InputConditionSet {
                    new KeyInputCondition(KeyCode.LeftArrow, true)
                }));

            // Move right
            DefineObjectAction<SMBPlayer>(
                p => !(bool)lockController.GetValue(p),
                (p, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new KeyInputCondition(KeyCode.RightArrow, true)
                }));

            // Jump
            DefineObjectAction<SMBPlayer>(
                p => !(bool)lockController.GetValue(p) && (bool)isOnGround.GetValue(p) && !InstrInput.GetKey(KeyCode.X),
                (p, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new KeyInputCondition(KeyCode.X, true)
                }));

            // Stop moving
            DefineAction(
                () => true,
                inputSim => inputSim.PerformAction(new InputConditionSet
                {
                    new KeyInputCondition(KeyCode.Z, false),
                    new KeyInputCondition(KeyCode.LeftArrow, false),
                    new KeyInputCondition(KeyCode.RightArrow, false),
                    new KeyInputCondition(KeyCode.X, false)
                }));
        }
    }
}
