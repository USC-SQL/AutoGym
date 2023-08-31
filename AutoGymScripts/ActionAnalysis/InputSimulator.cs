using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityActionAnalysis
{
    public class InputSimulator
    {
        private InputManagerSettings inputManagerSettings;
        private MonoBehaviour context;

        public InputSimulator(InputManagerSettings inputManagerSettings, MonoBehaviour context)
        {
            this.inputManagerSettings = inputManagerSettings;
            this.context = context;
            InstrInput.SetInputManagerSettings(inputManagerSettings);
            InstrInput.StartSimulation(context);
        }
        
        public void Reset()
        {
            InstrInput.ResetSimulatedInputs();
        }

        public void SimulateKeyDown(KeyCode keyCode)
        {
            InstrInput.SimulateKeyDown(keyCode);
        }

        public void SimulateKeyUp(KeyCode keyCode)
        {
            InstrInput.SimulateKeyUp(keyCode);
        }

        public void SimulateMouseX(float relX)
        {
            InstrInput.SimulateMouseX(relX);
        }

        public void SimulateMouseY(float relY)
        {
            InstrInput.SimulateMouseY(relY);
        }

        public void PerformAction(InputConditionSet inputConditions)
        {
            foreach (InputCondition cond in inputConditions)
            {
                cond.PerformInput(this, inputManagerSettings);
            }
        }

        public void Dispose()
        {
            InstrInput.StopSimulation();
        }
    }
}
