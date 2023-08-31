using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityActionAnalysis
{
    public static class CompileHelpers
    {

        public static new bool Equals(object x, object y)
        {
            object yc = ChangeType(y, x.GetType());
            return x.Equals(yc);
        }

        public static object ChangeType(object val, Type type)
        {
            if (type.IsEnum)
            {
                var enumUnderlying = type.GetEnumUnderlyingType();
                return Enum.ToObject(type, Convert.ChangeType(val, enumUnderlying));
            }
            else
            {
                return Convert.ChangeType(val, type);
            }
        }

        public static double ToDouble(object val)
        {
            return (double)Convert.ChangeType(val, typeof(double));
        }

        public static ulong ToUlong(object val)
        {
            return (ulong)Convert.ChangeType(val, typeof(ulong));
        }

        public static bool ToBool(object val)
        {
            return (bool)Convert.ChangeType(val, typeof(bool));
        }

        public static object IfThenElse(object cond, object trueVal, object falseVal)
        {
            return ToBool(cond) ? trueVal : falseVal;
        }

        public static object GetFieldValue(System.Reflection.FieldInfo field, object instance)
        {
            if (!field.IsStatic && instance == null)
            {
                throw new ResolutionException("Cannot get value of non-static field with null instance");
            }
            return field.GetValue(instance);
        }

        public static ulong GetArrayLength(object instance)
        {
            return (ulong)((object[])instance).Length;
        }

        public static ulong Xor(ulong a, ulong b)
        {
            return a ^ b;
        }

        public static ulong Shl(ulong a, ulong b)
        {
            return a << (int)b;
        }

        public static ulong Shr(ulong a, ulong b)
        {
            return a >> (int)b;
        }

        public static float InstanceMouseBoundsMinX(MonoBehaviour instance)
        {
            return (float)InstrInput.ExprSpecialVariables.InstanceMouseBoundsMinX(new ExprContext(instance));
        }

        public static float InstanceMouseBoundsMaxX(MonoBehaviour instance)
        {
            return (float)InstrInput.ExprSpecialVariables.InstanceMouseBoundsMaxX(new ExprContext(instance));
        }

        public static float InstanceMouseBoundsMinY(MonoBehaviour instance)
        {
            return (float)InstrInput.ExprSpecialVariables.InstanceMouseBoundsMinY(new ExprContext(instance));
        }

        public static float InstanceMouseBoundsMaxY(MonoBehaviour instance)
        {
            return (float)InstrInput.ExprSpecialVariables.InstanceMouseBoundsMaxY(new ExprContext(instance));
        }

        public static bool InstanceMouseDidEnter(MonoBehaviour instance)
        {
            return (bool)InstrInput.ExprSpecialVariables.InstanceMouseDidEnter(new ExprContext(instance));
        }

        public static bool InstanceMouseWasDown(MonoBehaviour instance)
        {
            return (bool)InstrInput.ExprSpecialVariables.InstanceMouseWasDown(new ExprContext(instance));
        }
    }
}
