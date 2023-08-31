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
    public class LeadsToInputAnalysisMethodResult
    {
        public ISet<ILInstruction> leadsToInputPoints;

        public LeadsToInputAnalysisMethodResult()
        {
            this.leadsToInputPoints = new HashSet<ILInstruction>();
        }
    }   

    public class LeadsToInputAnalysisResult
    {
        public Dictionary<string, LeadsToInputAnalysisMethodResult> methodResults;

        public LeadsToInputAnalysisResult()
        {
            this.methodResults = new Dictionary<string, LeadsToInputAnalysisMethodResult>();
        }
    }

    public class LeadsToInputAnalysis
    {
        private IMethod entryPoint;
        private InputAnalysisResult iaResult;
        private MethodPool pool;

        class MethodAnalysisState
        {
            public ControlFlowGraph cfg;
            public Dictionary<ILInstruction, bool> instIn;
            public Dictionary<ILInstruction, bool> instOut;

            public MethodAnalysisState()
            {
                cfg = null;
                instIn = new Dictionary<ILInstruction, bool>();
                instOut = new Dictionary<ILInstruction, bool>();
            }
        }

        public LeadsToInputAnalysis(IMethod entryPoint, InputAnalysisResult iaResult, MethodPool pool)
        {
            this.entryPoint = entryPoint;
            this.iaResult = iaResult;
            this.pool = pool;
        }

        public LeadsToInputAnalysisResult PerformAnalysis()
        {
            ISet<string> methodSigs = new HashSet<string>();
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

            Dictionary<string, MethodAnalysisState> methodStates = new Dictionary<string, MethodAnalysisState>();
            foreach (IMethod method in methods)
            {
                string methodSignature = AnalysisHelpers.MethodSignature(method);
                if (iaResult.methodResults.ContainsKey(methodSignature))
                {
                    ILFunction func = InstructionPointer.FindInstructionFunction(pool.MethodEntryPoint(method).block);
                    ControlFlowGraph cfg = new ControlFlowGraph((BlockContainer)func.Body);
                    MethodAnalysisState mst = new MethodAnalysisState();
                    mst.cfg = cfg;
                    foreach (ILInstruction inst in AnalysisHelpers.Instructions(func))
                    {
                        mst.instIn[inst] = false;
                        mst.instOut[inst] = false;
                    }
                    methodStates[methodSignature] = mst;
                }
            }

            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (IMethod method in methods)
                {
                    string methodSignature = AnalysisHelpers.MethodSignature(method);
                    if (iaResult.methodResults.TryGetValue(methodSignature, out InputAnalysisMethodResult iaMethodResult))          
                    {
                        MethodAnalysisState mst = methodStates[methodSignature];
                        ControlFlowGraph cfg = mst.cfg;
                        ILFunction func = InstructionPointer.FindInstructionFunction(pool.MethodEntryPoint(method).block);
                        foreach (ILInstruction inst in AnalysisHelpers.Instructions(func))
                        {
                            bool instIn = false;
                            foreach (ILInstruction succ in AnalysisHelpers.Successors(inst, cfg))
                            {
                                if (mst.instOut[succ])
                                {
                                    instIn = true;
                                    break;
                                }
                            }
                            mst.instIn[inst] = instIn;

                            bool gen = iaMethodResult.inputDependentPoints.Contains(inst);
                            if (!gen)
                            {
                                if (AnalysisHelpers.FindCallInstruction(inst, out CallInstruction callinst))
                                {
                                    IMethod targetMethod = callinst.Method;
                                    string targetMethodSignature = AnalysisHelpers.MethodSignature(targetMethod);
                                    if (methodStates.TryGetValue(targetMethodSignature, out MethodAnalysisState targetMst))
                                    {
                                        foreach (var entry in targetMst.instOut)
                                        {
                                            if (entry.Value)
                                            {
                                                gen = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            bool instOut = instIn || gen;
                            if (instOut != mst.instOut[inst])
                            {
                                changed = true;
                                mst.instOut[inst] = instOut;
                            }
                        }
                    }
                }
            }

            LeadsToInputAnalysisResult res = new LeadsToInputAnalysisResult();
            foreach (IMethod method in methods)
            {
                string methodSignature = AnalysisHelpers.MethodSignature(method);
                if (methodStates.TryGetValue(methodSignature, out var mst))
                {
                    LeadsToInputAnalysisMethodResult mres = new LeadsToInputAnalysisMethodResult();
                    foreach (var entry in mst.instOut)
                    {
                        if (entry.Value)
                        {
                            mres.leadsToInputPoints.Add(entry.Key);
                        }
                    }
                    res.methodResults[methodSignature] = mres;
                }
            }

            return res;
        }
    }
}