using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.ControlFlow;
using ICSharpCode.Decompiler.FlowAnalysis;

namespace UnityActionAnalysis
{
    public class InputAnalysisMethodResult
    {
        public ISet<ILInstruction> inputDependentPoints;

        public InputAnalysisMethodResult()
        {
            this.inputDependentPoints = new HashSet<ILInstruction>();
        }
    }

    public class InputAnalysisResult
    {
        public Dictionary<string, InputAnalysisMethodResult> methodResults;

        public InputAnalysisResult()
        {
            this.methodResults = new Dictionary<string, InputAnalysisMethodResult>();
        }
    }

    public class InputAnalysis
    {
        private IMethod entryPoint;
        public readonly MethodPool pool;

        public IMethod EntryPoint { get => entryPoint; }

        class NodeFlowState
        {
            public ISet<string> variables;

            public NodeFlowState()
            {
                variables = new HashSet<string>();
            }

            public void AddAll(NodeFlowState other)
            {
                foreach (string v in other.variables)
                {
                    variables.Add(v);
                }
            }

            public override bool Equals(object other)
            {
                if (other is NodeFlowState s)
                {
                    return variables.SetEquals(s.variables);
                } else 
                {
                    return false;
                }
            }

            public override int GetHashCode()
            {
                int hash = 1;
                foreach (string v in variables)
                {
                    hash = HashCode.Combine(hash, v.GetHashCode());
                }
                return hash;
            }
        }

        class MethodAnalysisState
        {
            public ControlFlowGraph cfg;
            public Dictionary<ILInstruction, NodeFlowState> instIn;
            public Dictionary<ILInstruction, NodeFlowState> instOut;
            public ISet<string> inputParams;

            public MethodAnalysisState()
            {
                cfg = null;
                instIn = new Dictionary<ILInstruction, NodeFlowState>();
                instOut = new Dictionary<ILInstruction, NodeFlowState>();
                inputParams = new HashSet<string>();
            }
        }

        class AnalysisState
        {
            public Dictionary<string, MethodAnalysisState> methodStates;
            public ISet<string> inputMethods;
            public ISet<string> inputFields;

            public AnalysisState()
            {
                methodStates = new Dictionary<string, MethodAnalysisState>();
                inputMethods = new HashSet<string>();
                inputFields = new HashSet<string>();
            }
        }

        public InputAnalysis(IMethod entryPoint, MethodPool pool)
        {
            this.entryPoint = entryPoint;
            this.pool = pool;
        }

        private bool ContainsInput(ILInstruction val, ILInstruction inst, IMethod contextMethod, AnalysisState st)
        {
            string contextMethodSignature = AnalysisHelpers.MethodSignature(contextMethod);
            MethodAnalysisState mst = st.methodStates[contextMethodSignature];
            return AnalysisHelpers.ExpressionContainsAnyInputAPI(val)
                || AnalysisHelpers.ExpressionContainsAnyMethod(val, st.inputMethods)
                || AnalysisHelpers.ExpressionContainsAnyField(val, st.inputFields)
                || AnalysisHelpers.ExpressionContainsAnyVariable(val, mst.inputParams)
                || AnalysisHelpers.ExpressionContainsAnyVariable(val, mst.instIn[inst].variables);
        }

        public InputAnalysisResult PerformAnalysis()
        {
            ISet<string> methodSigs = new HashSet<String>();
            ISet<IMethod> methods = new HashSet<IMethod>();
            ReachableMethods rm = new ReachableMethods(entryPoint, pool);
            foreach (IMethod m in rm.FindReachableMethods())
            {
                string methodSignature = AnalysisHelpers.MethodSignature(m);
                if (!methodSigs.Contains(methodSignature) && AnalysisHelpers.ShouldProcessBody(m, entryPoint))
                {
                    methods.Add(m);
                    methodSigs.Add(methodSignature);
                }
            }

            AnalysisState st = new AnalysisState();

            // initialize
            foreach (IMethod method in methods)
            {
                string methodSignature = AnalysisHelpers.MethodSignature(method);
                MethodAnalysisState mst = new MethodAnalysisState();
                ILFunction func = InstructionPointer.FindInstructionFunction(pool.MethodEntryPoint(method).block);
                mst.cfg = new ControlFlowGraph((BlockContainer)func.Body);
                foreach (ILInstruction inst in AnalysisHelpers.Instructions(func))
                {
                    mst.instIn.Add(inst, new NodeFlowState());
                    mst.instOut.Add(inst, new NodeFlowState());
                }
                st.methodStates.Add(methodSignature, mst);
            }

            // run analysis
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (IMethod method in methods)
                {
                    string methodSignature = AnalysisHelpers.MethodSignature(method);
                    MethodAnalysisState mst = st.methodStates[methodSignature];
                    ILFunction func = InstructionPointer.FindInstructionFunction(pool.MethodEntryPoint(method).block);
                    foreach (ILInstruction inst in AnalysisHelpers.Instructions(func))
                    {
                        NodeFlowState newIn = new NodeFlowState();
                        foreach (ILInstruction pred in AnalysisHelpers.Predecessors(inst, mst.cfg))
                        {
                            newIn.AddAll(mst.instOut[pred]);
                        }
                        mst.instIn[inst] = newIn;
                        NodeFlowState newOut = new NodeFlowState();
                        newOut.AddAll(newIn);

                        if (inst is StLoc stloc)
                        {
                            string assignVar = stloc.Variable.Name;

                            // kill
                            if (newOut.variables.Contains(assignVar))
                            {
                                newOut.variables.Remove(assignVar);
                            }

                            // gen
                            ILInstruction val = stloc.Value;
                            if (val is CallInstruction callinst && AnalysisHelpers.ShouldProcessBody(callinst.Method, entryPoint))
                            {
                                if (AnalysisHelpers.ExpressionContainsAnyMethod(callinst, st.inputMethods))
                                {
                                    newOut.variables.Add(assignVar);
                                } 
                            } else if (ContainsInput(stloc.Value, inst, method, st))
                            {
                                newOut.variables.Add(assignVar);
                            }
                        } 

                        if (!newOut.Equals(mst.instOut[inst]))
                        {
                            mst.instOut[inst] = newOut;
                            changed = true;
                        }
                    }
                    foreach (ILInstruction inst in AnalysisHelpers.Instructions(func))
                    {
                        if (inst is Leave leave)
                        {
                            if (ContainsInput(leave.Value, inst, method, st))
                            {
                                if (!st.inputMethods.Contains(methodSignature))
                                {
                                    st.inputMethods.Add(methodSignature);
                                    changed = true;
                                }
                            }
                        } else if (inst is StObj stobj && (stobj.Target is IInstructionWithFieldOperand fop))
                        {
                            if (ContainsInput(stobj.Value, inst, method, st))
                            {
                                string fieldSignature = AnalysisHelpers.FieldSignature(fop.Field);
                                if (!st.inputFields.Contains(fieldSignature))
                                {
                                    st.inputFields.Add(fieldSignature);
                                    changed = true;
                                }
                            }
                        } else if (AnalysisHelpers.FindCallInstruction(inst, out CallInstruction callinst))
                        {
                            IMethod targetMethod = callinst.Method;
                            string targetMethodSignature = AnalysisHelpers.MethodSignature(targetMethod);
                            if (st.methodStates.ContainsKey(targetMethodSignature))
                            {
                                MethodAnalysisState targetMst = st.methodStates[targetMethodSignature];
                                for (int argIndex = 0, numArgs = callinst.Arguments.Count; argIndex < numArgs; ++argIndex)
                                {
                                    ILInstruction argValue = callinst.Arguments[argIndex];
                                    if (ContainsInput(argValue, inst, method, st))
                                    {
                                        var argParam = callinst.GetParameter(argIndex);
                                        string param = argParam != null ? argParam.Name : "this";
                                        if (!targetMst.inputParams.Contains(param))
                                        {
                                            targetMst.inputParams.Add(param);
                                            changed = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // produce results
            InputAnalysisResult result = new InputAnalysisResult();
            foreach (IMethod method in methods)
            {
                InputAnalysisMethodResult mr = new InputAnalysisMethodResult();
                ILFunction func = InstructionPointer.FindInstructionFunction(pool.MethodEntryPoint(method).block);
                foreach (ILInstruction inst in AnalysisHelpers.Instructions(func))
                {
                    if (ContainsInput(inst, inst, method, st))
                    {
                        mr.inputDependentPoints.Add(inst);
                    }
                }
                result.methodResults.Add(AnalysisHelpers.MethodSignature(method), mr);
            }

            return result;
        }
    }
}
