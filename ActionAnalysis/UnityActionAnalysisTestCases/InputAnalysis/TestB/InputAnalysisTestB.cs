using System;
using UnityEngine;

namespace UnityActionAnalysisTestCases.InputAnalysis.TestB
{
    struct MouseData {
        public float mouseX;
        public float mouseY;      
        public float extra; 
    }

    public class ProgramB
    {

        MouseData ProcessMouseCoords(Vector3 mouseCoords)
        {
            MouseData res = new MouseData();
            res.mouseX = mouseCoords.x / 2.0f; // *
            res.mouseY = mouseCoords.y / 3.0f; // *
            res.extra = 2.0f;
            return res;
        }

        float f(int n, string s) {
            float x = 1.0f;
            for (int i = 0; i < n; ++i) {
                x += s[i];
            }
            return x;
        }

        float g(bool b) {
            Console.WriteLine(b); // *
            return 2.0f;
        }

        void Update()
        {
            Vector3 mousePos = Input.mousePosition;  // *
            MouseData md = ProcessMouseCoords(mousePos); // *
            float x = md.mouseX + 1.0f; // *
            float y = md.mouseY + 2.0f; // *
            if (md.extra > 0.0f) {
                Console.WriteLine(x); // *
                Console.WriteLine(y); // *
                Console.WriteLine(md.extra);
                Console.WriteLine(g(Input.GetButton("Fire"))); // *
                Console.WriteLine(f(3, "abc"));
            }
        }
    }
}