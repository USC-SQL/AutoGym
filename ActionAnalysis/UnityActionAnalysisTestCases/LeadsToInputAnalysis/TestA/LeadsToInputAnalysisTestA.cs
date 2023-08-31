using System;
using UnityEngine;

namespace UnityActionAnalysisTestCases.LeadsToInputAnalysis.TestA
{
    public class ProgramA
    {
        void Update()
        {
            Console.WriteLine("A");
            Console.WriteLine("B");
            Console.WriteLine(Input.GetAxis("Horizontal"));
            Console.WriteLine("C");
            Console.WriteLine("D");
        }
    }
}
