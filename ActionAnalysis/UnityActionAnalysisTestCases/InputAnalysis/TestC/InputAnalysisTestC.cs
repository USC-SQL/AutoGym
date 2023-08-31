using System;
using UnityEngine;

namespace UnityActionAnalysisTestCases.InputAnalysis.TestC
{
    struct ExtraData
    {
        public float zf; 
    }

    public class ProgramC
    {
        int xf; 
        static float yf; 
        static ExtraData d;
        static float wf;
        static string qf; 
        static string qf2;

        void h() {
            if (xf + yf > 0.0f) { // *
                qf = d.zf.ToString(); // *
                qf2 = "x";
            } else {
                qf = "a";
                qf2 = "y";
            }
        }

        void f()
        {
            d.zf = Input.GetAxis("Mouse X"); // *
            if (d.zf > 0.0f) { // *
                h();
            }
        }

        void g() {
            Console.WriteLine("g()! " + Input.GetAxis("Horizontal"));
        }

        void k(string s) {
            Console.WriteLine(s); // *
        }

        void Update()
        {
            xf = (int)(Input.GetAxis("Horizontal")*1000.0f); // *
            yf = Input.GetAxis("Vertical")*20.0f; // *
            d = new ExtraData();
            f();
            if (d.zf > 0.0f) { // *
                wf = 1.0f;
            } else {
                wf = 2.0f;
            }
            k(qf); // *
            Console.WriteLine(xf); // *
            Console.WriteLine(yf); // *
            Console.WriteLine(wf);
            Console.WriteLine(qf); // *
            Console.WriteLine(qf2);
        }
    }
}