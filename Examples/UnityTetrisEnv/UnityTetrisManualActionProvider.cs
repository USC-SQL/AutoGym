using System;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityActionAnalysis;
using TetrisEngine;

namespace UnityRLEnv
{
    public class UnityTetrisManualActionProvider : ManualActionProviderBase
    {
        protected override void DefineActions()
        {
            FieldInfo gameIsOver = typeof(GameLogic).GetField("mGameIsOver", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo gameSettings = typeof(GameLogic).GetField("mGameSettings", BindingFlags.NonPublic | BindingFlags.Instance);

            Func<GameLogic, GameSettings> getGameSettings = (GameLogic l) => (GameSettings)gameSettings.GetValue(l);

            // Rotate right
            DefineObjectAction<GameLogic>(
                l => !InstrInput.GetKey(getGameSettings(l).rotateRightKey)
                    && !(bool)gameIsOver.GetValue(l),
                (l, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new KeyInputCondition(getGameSettings(l).rotateRightKey, true)
                }));

            // Rotate left
            DefineObjectAction<GameLogic>(
                l => !InstrInput.GetKey(getGameSettings(l).rotateLeftKey)
                    && !(bool)gameIsOver.GetValue(l),
                (l, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new KeyInputCondition(getGameSettings(l).rotateLeftKey, true)
                }));

            // Move left
            DefineObjectAction<GameLogic>(
                l => !InstrInput.GetKey(getGameSettings(l).moveLeftKey)
                    && !(bool)gameIsOver.GetValue(l),
                (l, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new KeyInputCondition(getGameSettings(l).moveLeftKey, true)
                }));

            // Move right
            DefineObjectAction<GameLogic>(
                l => !InstrInput.GetKey(getGameSettings(l).moveRightKey)
                    && !(bool)gameIsOver.GetValue(l),
                (l, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new KeyInputCondition(getGameSettings(l).moveRightKey, true)
                }));

            // Fall faster
            DefineObjectAction<GameLogic>(
                l => !(bool)gameIsOver.GetValue(l),
                (l, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new KeyInputCondition(getGameSettings(l).moveDownKey, true)
                }));

            // Stop movement
            DefineObjectAction<GameLogic>(
                l => true,
                (l, inputSim) => inputSim.PerformAction(new InputConditionSet
                {
                    new KeyInputCondition(getGameSettings(l).rotateRightKey, false),
                    new KeyInputCondition(getGameSettings(l).rotateLeftKey, false),
                    new KeyInputCondition(getGameSettings(l).moveLeftKey, false),
                    new KeyInputCondition(getGameSettings(l).moveRightKey, false),
                    new KeyInputCondition(getGameSettings(l).moveDownKey, false),
                }));
        }
    }
}
