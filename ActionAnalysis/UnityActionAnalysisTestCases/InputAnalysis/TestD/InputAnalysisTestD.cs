using System;
using UnityEngine;

namespace UnityActionAnalysisTestCases.InputAnalysis.TestD
{
   
    public class Obj
    {
        public float Prop { 
            get; // *
            set; // *
        }

        public float Prop2 { 
            get => Prop; // *
        }
    }

    public class ProgramD
    {
        private Obj obj;

        float f() {
            return obj.Prop2; // *
        }

        float g1() {
            return Input.GetAxis("Horizontal"); // *
        }

        bool g2() {
            return Input.GetButton("Fire"); // *
        }

        void FixedUpdate()
        {
            obj = new Obj();
            obj.Prop = g1(); // *
            Console.WriteLine(obj.Prop); // *
            try 
            {
                Console.WriteLine(f()); // *
                Console.WriteLine(g2()); // *
            } catch (Exception) {
                Console.WriteLine(Input.GetAxis("Vertical"));
            }
        }
    }
}