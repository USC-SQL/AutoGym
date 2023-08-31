#define LOG_RESOLUTION_WARNINGS

using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.Z3;

namespace UnityActionAnalysis
{
    public class SymexPath
    {
        public readonly int pathId;
        public readonly int pathIndex;
        private BoolExpr[] condition;
        public readonly Dictionary<int, Symcall> symcalls;
        private Dictionary<FuncDecl, Func<ExprContext, object>> nonInputVars;
        public readonly Dictionary<int, List<Func<ExprContext, object>>> inputArgs;
        private Context z3;
        
        public SymexMethod Method { get; private set; }

        public SymexPath(int pathId, int pathIndex, BoolExpr[] condition, Dictionary<int, Symcall> symcalls, SymexMethod m, Context z3)
        {
            this.pathId = pathId;
            this.pathIndex = pathIndex;
            this.condition = condition;
            this.symcalls = symcalls;

            Method = m;

            inputArgs = new Dictionary<int, List<Func<ExprContext, object>>>();

            nonInputVars = new Dictionary<FuncDecl, Func<ExprContext, object>>();
            var freeVars = SymexHelpers.FindFreeVariables(condition);
            foreach (FuncDecl variable in freeVars)
            {
                if (IsInputVariable(variable))
                {
                    if (GetSymcallIdFromVarName(variable.Name.ToString(), out int symcallId))
                    {
                        List<Func<ExprContext, object>> args = new List<Func<ExprContext, object>>();
                        Symcall sc = symcalls[symcallId];
                        if (!inputArgs.ContainsKey(symcallId)) // may already be present if variable is accessing a field from the result of a symcall
                        {
                            foreach (SymexValue arg in sc.args)
                            {
                                Func<ExprContext, object> compiled = null;
                                try
                                {
                                    compiled = ExprCompile.ResolveValue(arg, this);
                                }
#if LOG_RESOLUTION_WARNINGS
                                catch (ResolutionException e)
                                {
                                    Debug.LogWarning("failed to resolve value '" + arg + "' due to: " + e.Message);
                                }
#else
                            catch (ResolutionException) { }
#endif
                                args.Add(compiled);
                            }
                            inputArgs.Add(symcallId, args);
                        }
                    }
                } else
                {
                    try
                    {
                        var fn = ExprCompile.ResolveVariable(variable.Name.ToString(), this);
                        nonInputVars.Add(variable, fn);
                    }
#if LOG_RESOLUTION_WARNINGS
                    catch (ResolutionException e)
                    {
                        Debug.LogWarning("failed to resolve variable '" + variable + "' due to: " + e.Message);
                    }
#else
                    catch (ResolutionException) { }
#endif
                }
            }

            this.z3 = z3;
        }

        public static bool GetSymcallIdFromVarName(string variableName, out int symcallId)
        {
            if (variableName.StartsWith("symcall:"))
            {
                int secondColon = variableName.IndexOf(':', 8);
                int idLength = secondColon < 0 ? variableName.Length - 8 : secondColon - 8;
                symcallId = int.Parse(variableName.Substring(8, idLength));
                return true;
            }
            else
            {
                symcallId = -1;
                return false;
            }
        }

        public static string GetSymcallAccessorInVarName(string variableName)
        {
            int accessorSepIndex = variableName.IndexOf(':', 8);
            if (accessorSepIndex < 0)
            {
                return null;
            }
            return variableName.Substring(accessorSepIndex+1);
        }

        public bool IsInputVariable(FuncDecl variable)
        {
            string name = variable.Name.ToString();
            if (GetSymcallIdFromVarName(name, out int symcallId))
            {
                Symcall sc = symcalls[symcallId];
                if (sc.method.DeclaringType.FullName == "UnityEngine.Input")
                {
                    return true;
                }
            }
            return false;
        }

        public bool ContainsInputVariable(Expr e)
        {
            if (e.IsConst && e.FuncDecl.DeclKind == Z3_decl_kind.Z3_OP_UNINTERPRETED && IsInputVariable(e.FuncDecl))
            {
                return true;
            } else
            {
                for (uint i = 0, n = e.NumArgs; i < n; ++i)
                {
                    if (ContainsInputVariable(e.Arg(i)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool CheckFeasible(MonoBehaviour instance, IPreconditionFuncs pfuncs)
        {
            var pfunc = pfuncs.Preconditions[Method.method][pathIndex - 1];
            try
            {
                return pfunc(instance);
            }
#if LOG_RESOLUTION_WARNINGS
            catch (ResolutionException e)
            {
                Debug.LogWarning("Failed to evaluate precondition function for " + Method.method.DeclaringType.FullName + "." + Method.method.Name + " with path index " + pathIndex + " due to: " + e.Message);
                return false;
            }
#else
            catch (ResolutionException)
            {
                return false;
            }
#endif
        }

        private InputCondition ModelInputVariableToCondition(Model m, FuncDecl varDecl, Expr value, ExprContext evalContext, Context z3)
        {
            string name = varDecl.Name.ToString();
            if (GetSymcallIdFromVarName(name, out int symcallId))
            {
                Symcall sc = symcalls[symcallId];
                if (sc.method.DeclaringType.FullName == "UnityEngine.Input")
                {
                    if (sc.method.Name == "GetAxis" || sc.method.Name == "GetAxisRaw")
                    {
                        var arg = inputArgs[symcallId][0];
                        if (arg == null)
                        {
                            throw new ResolutionException("axis name unavailable");
                        }
                        var result = arg(evalContext);
                        if (!(result is string))
                        {
                            throw new ResolutionException("unexpected result from evaluating axis argument: " + result);
                        }
                        string axisName = (string)result;
                        var zero = z3.MkReal(0);
                        var one = z3.MkReal(1);
                        var negOne = z3.MkReal(-1);
                        float axisValue = (float)m.Double(z3.MkITE(z3.MkGt((RealExpr)value, zero), one, z3.MkITE(z3.MkLt((RealExpr)value, zero), negOne, zero)));
                        return new AxisInputCondition(axisName, axisValue);
                    } else if (sc.method.Name == "GetKey")
                    {
                        var arg = inputArgs[symcallId][0];
                        if (arg == null)
                        {
                            throw new ResolutionException("key code unavailable");
                        }
                        KeyCode keyCode;
                        object obj = arg(evalContext);
                        if (obj is string)
                        {
                            keyCode = InputManagerSettings.KeyNameToCode((string)obj).Value;
                        } else
                        {
                            int keyCodeVal = (int)Convert.ChangeType(obj, typeof(int));
                            keyCode = (KeyCode)Enum.ToObject(typeof(KeyCode), keyCodeVal);
                        }
                        uint intVal = uint.Parse(value.ToString());
                        return new KeyInputCondition(keyCode, intVal != 0);
                    } else if (sc.method.Name == "GetButton")
                    {
                        var arg = inputArgs[symcallId][0];
                        if (arg == null)
                        {
                            throw new ResolutionException("button name unavailable");
                        }
                        var result = arg(evalContext);
                        if (!(result is string))
                        {
                            throw new ResolutionException("unexpected result from evaluating button argument: " + result);
                        }
                        string buttonName = (string)result;
                        uint intVal = uint.Parse(value.ToString());
                        return new ButtonInputCondition(buttonName, intVal != 0);
                    } else if (sc.method.Name == "GetMouseButton")
                    {
                        var arg = inputArgs[symcallId][0];
                        if (arg == null)
                        {
                            throw new ResolutionException("mouse button number unavailable");
                        }
                        var result = arg(evalContext);
                        if (!(result is ulong))
                        {
                            throw new ResolutionException("unexpected result from evaluating mouse button argument: " + result);
                        }
                        int mouseButton = (int)((ulong)result);
                        uint intVal = uint.Parse(value.ToString());
                        if (mouseButton < 0 || mouseButton > 6)
                        {
                            throw new ResolutionException("unsupported mouse button: " + mouseButton);
                        }
                        KeyCode keyCode = KeyCode.Mouse0 + mouseButton;
                        return new KeyInputCondition(keyCode, intVal != 0);
                    } else if (sc.method.Name == "get_mousePosition")
                    {
                        string accessor = GetSymcallAccessorInVarName(name);
                        MousePositionInputCondition.VectorAxis axis;
                        switch (accessor)
                        {
                            case "instancefield:x":
                                axis = MousePositionInputCondition.VectorAxis.X_AXIS;
                                break;
                            case "instancefield:y":
                                axis = MousePositionInputCondition.VectorAxis.Y_AXIS;
                                break;
                            default:
                                throw new ResolutionException("unexpected accessor on mouse position: " + accessor);
                        }
                        float posValue = (float)m.Double(value);
                        return new MousePositionInputCondition(axis, posValue);
                    } else
                    {
                        throw new ResolutionException("unsupported input API " + sc.method.DeclaringType.FullName + "." + sc.method.Name);
                    }
                }
                else
                {
                    throw new ResolutionException("unrecognized input symcall to method " + sc.method.Name + " in " + sc.method.DeclaringType.FullName);
                }
            }
            throw new ResolutionException("unrecognized input variable '" + name + "'");
        }

        private void ModelToInputConditions(Model m, ExprContext evalContext, Context z3, out InputConditionSet inputConditions)
        {
            inputConditions = new InputConditionSet();
            foreach (var p in m.Consts)
            {
                var decl = p.Key;
                var value = p.Value;
                if (IsInputVariable(decl))
                {
                    InputCondition cond = ModelInputVariableToCondition(m, decl, value, evalContext, z3);
                    inputConditions.Add(cond);
                }
            }
        }

        public bool SolveForInputs(MonoBehaviour instance, out InputConditionSet result)
        {
            ExprContext ctx = new ExprContext(instance);
            using (var solver = z3.MkSolver())
            {
                solver.Assert(condition);
                foreach (var kv in nonInputVars)
                {
                    FuncDecl v = kv.Key;
                    Func<ExprContext, object> fn = kv.Value;
                    try
                    {
                        object value = fn(ctx);
                        var assertion = z3.MkEq(z3.MkConst(v.Name, v.Range), SymexHelpers.ToZ3Expr(value, v.Range, z3));
                        solver.Assert(assertion);
                    }
#if LOG_RESOLUTION_WARNINGS
                    catch (ResolutionException e)
                    {
                        Debug.LogWarning("failed to evaluate variable " + v.Name.ToString() + " due to: " + e.Message);
                        result = null;
                        return false;
                    }
#else
                    catch (ResolutionException)
                    {
                        result = null;
                        return false;
                    }
#endif
                }
                if (solver.Check() == Status.SATISFIABLE)
                {
                    try
                    {
                        ModelToInputConditions(solver.Model, ctx, z3, out result);
                        return true;
                    }
#if LOG_RESOLUTION_WARNINGS
                    catch (ResolutionException e)
                    {
                        Debug.LogWarning("failed to resolve input variables (action will have no effect): " + e.Message);
                        result = null;
                        return false;
                    }
#else
                    catch (ResolutionException)
                    {
                        result = null;
                        return false;
                    }
#endif
                }
                else
                {
                    result = null;
                    return false;
                }
            }
        }
    }
}