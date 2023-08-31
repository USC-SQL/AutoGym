using System;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityActionAnalysis;
using com.javierquevedo;

namespace UnityRLEnv
{
    public class BubbleShooterManualActionProvider : ManualActionProviderBase
    {
        protected override void DefineActions()
        {
            FieldInfo isPlaying = typeof(BubbleMatrixController).GetField("_isPlaying", BindingFlags.NonPublic | BindingFlags.Instance);

            // Aim left
            DefineObjectAction<BubbleShooterController>(
                bsc => bsc.isAiming,
                (bsc, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new AxisInputCondition("Mouse X", -1.0f)
                }));

            // Hold aim
            DefineObjectAction<BubbleShooterController>(
                bsc => bsc.isAiming,
                (bsc, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new AxisInputCondition("Mouse X", 0.0f)
                }));

            // Aim right
            DefineObjectAction<BubbleShooterController>(
                bsc => bsc.isAiming,
                (bsc, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new AxisInputCondition("Mouse X", 1.0f)
                }));

            // Shoot
            DefineObjectAction<BubbleMatrixController>(
                bmc => (bool)isPlaying.GetValue(bmc) && !InstrInput.GetMouseButton(0),
                (bmc, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new KeyInputCondition(KeyCode.Mouse0, true)
                }));

            // Stop shooting
            DefineAction(
                () => true,
                inputSim => inputSim.PerformAction(new InputConditionSet
                {
                    new KeyInputCondition(KeyCode.Mouse0, false)
                }));
        }
    }
}
