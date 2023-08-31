using System;
using UnityEngine;

namespace UnityActionAnalysisTestCases.DependenceGraph.TestA
{

    public class ProgramA
    {
        private static int val;

        void Update()
        {
            float x = Input.GetAxis("Horizontal");
            float y = Math.Abs(x);
            if (y > 0.0f)
            {
                MyObject obj = new MyObject(y);
                val = (int)(x*100.0f);
                float z = f(obj);
                Console.WriteLine(z);
            }
        }

        float f(MyObject obj)
        {
            float z = obj.GetValue() + 1.0f;
            if (val > 0) {
                return z;
            } else {
                return -z;
            }
        }
    }

}