using System;
using UnityEngine;

namespace UnityActionAnalysisTestCases.DependenceGraph.TestB
{
    /* Note: Currently unused, may be used in a later test case */
    public class ProgramB
    {
        private float horiz;
        private float vert;
        private float x;
        private float y;

        public void ReadInputs()
        {
            horiz = Input.GetAxis("Horizontal");
            vert = Input.GetAxis("Vertical");
        }

        public bool AnyInput()
        {
            float h = Math.Abs(horiz);
            float v = Math.Abs(vert);
            return h > 0 || v > 0;
        }

        public void Update()
        {
            ReadInputs();
            Move();
        }

        public void Move()
        {
            if (AnyInput())
            {
                x += horiz * Time.deltaTime;
                y += vert * Time.deltaTime;
            }
        }
    }

}