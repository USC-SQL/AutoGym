using System;
using UnityEngine;

namespace UnityActionAnalysisTestCases.LeadsToInputAnalysis.TestB
{
    public class ProgramB
    {
        void CheckInput()
        {
            if (Input.GetAxis("Horizontal") > 0.0f)
            {
                Console.WriteLine("X");
            }
        }

        void Update()
        {
            Console.WriteLine("A");
            Console.WriteLine("B");
            CheckInput();
            Console.WriteLine("C");
        }
    }
}
