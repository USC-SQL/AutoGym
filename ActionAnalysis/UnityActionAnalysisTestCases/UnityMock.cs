using System;
using System.Collections.Generic;
using System.Text;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x;
        public float y;
        public float z;
    }

    public class Input
    {
        public static Vector3 mousePosition
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public static bool GetButton(string buttonDown)
        {
            throw new NotImplementedException();
        }

        public static float GetAxis(string axisName)
        {
            throw new NotImplementedException();
        }
    }

    public class Time
    {
        public static float deltaTime { get => throw new NotImplementedException(); }
    }
}