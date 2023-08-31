using System;
using UnityEngine;

namespace UnityActionAnalysisTestCases.InputAnalysis.TestA
{
    public class ProgramA
    {
        void f(float x) 
        {
            Console.WriteLine(x); // *
            if (x < 0.0f) { // *
                Console.WriteLine("N");
            }
        }

        float g(float x)
        {
            if (x > 0) // *
            {
                return 1.0f;
            } else
            {
                return -1.0f;
            }
        }

        void Update()
        {
            bool b = Input.GetButton("h"); // *
            if (b) { // *
                string msg = GetHashCode() > 0  ? "A" : "B";
                Console.WriteLine(msg);
            }

            float x;
            if (GetHashCode() > 0) {
                x = Input.GetAxis("Horizontal"); // *
            } else {
                x = 1.0f;
            }

            f(x); // *

            float y = g(x); // *
            Console.WriteLine(y);
        }
    }
}
