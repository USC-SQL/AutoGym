using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.ControlFlow;
using ICSharpCode.Decompiler.CSharp;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace UnityActionAnalysis
{
    public class InputSlicer
    {
        private static string MethodSignature(IMethod m)
        {
            return m.DeclaringType.FullName + "." + m.Name + "(" + string.Join(";", m.Parameters.Select(param => param.Type.FullName)) + ")";
        }

        private static string MethodSignature(MethodDefinition md)
        {
            return md.DeclaringType.FullName + "." + md.Name + "(" + string.Join(";", md.Parameters.Select(param => param.ParameterType.FullName)) + ")";
        }

        private static Dictionary<string, ISet<int>> ComputePartialSlice(IMethod entryPoint, CSharpDecompiler decompiler, MethodPool pool)
        {
            Console.WriteLine("\t Computing slice for " + entryPoint.FullName);
            InputAnalysis ia = new InputAnalysis(entryPoint, pool);
            DepGraphAnalysis dga = new DepGraphAnalysis(entryPoint, pool);
            InputAnalysisResult iaResult = ia.PerformAnalysis();
            DepGraph dg = dga.PerformAnalysis();
            ISet<DepGraphNode> slice = new HashSet<DepGraphNode>();
            ISet<DepGraphNode> workSet = new HashSet<DepGraphNode>();
            foreach (InputAnalysisMethodResult res in iaResult.methodResults.Values)
            {
                foreach (ILInstruction inst in res.inputDependentPoints)
                {
                    workSet.Add(new DepGraphInstructionNode(inst));
                }
            }
            while (workSet.Count > 0)
            {
                DepGraphNode node = workSet.First();
                workSet.Remove(node);
                slice.Add(node);
                foreach (DepGraphDirectedEdge edge in dg.GetIncomingEdges(node))
                {
                    DepGraphNode source = edge.Source;
                    if (!slice.Contains(source))
                    {
                        workSet.Add(source);
                    }
                }
            }
            Dictionary<string, ISet<int>> result = new Dictionary<string, ISet<int>>();
            foreach (DepGraphNode node in slice)
            {
                if (node is DepGraphInstructionNode instNode)
                {
                    ILInstruction inst = (ILInstruction)instNode.NodeObject;
                    IMethod method = InstructionPointer.FindInstructionFunction(inst).Method;
                    string methodSig = MethodSignature(method);
                    ISet<int> methodSlice;
                    if (!result.TryGetValue(methodSig, out methodSlice))
                    {
                        methodSlice = new HashSet<int>();
                        result.Add(methodSig, methodSlice);
                    }
                    foreach (var ivl in inst.ILRanges)
                    {
                        for (int ilAddr = ivl.Start; ilAddr <= ivl.InclusiveEnd; ++ilAddr)
                        {
                            methodSlice.Add(ilAddr);
                        }
                    }
                } else if (node is DepGraphMethodNode methodNode)
                {
                    IMethod method = (IMethod)methodNode.NodeObject;
                    string methodSig = MethodSignature(method);
                    if (!result.ContainsKey(methodSig))
                    {
                        result.Add(methodSig, new HashSet<int>());
                    }
                }
            }
            return result;
        }

        private static Dictionary<string, ISet<int>> ComputeSlice(IEnumerable<IMethod> entryPoints, CSharpDecompiler decompiler, MethodPool pool)
        {
            Dictionary<string, ISet<int>> slice = new Dictionary<string, ISet<int>>();
            foreach (IMethod entryPoint in entryPoints)
            {
                var partialSlice = ComputePartialSlice(entryPoint, decompiler, pool);
                foreach (var entry in partialSlice)
                {
                    string methodSig = entry.Key;
                    ISet<int> partialMethodSlice = entry.Value;
                    ISet<int> methodSlice;
                    if (!slice.TryGetValue(methodSig, out methodSlice))
                    {
                        methodSlice = new HashSet<int>();
                        slice.Add(methodSig, methodSlice);
                    }
                    foreach (int ilAddr in partialMethodSlice)
                    {
                        methodSlice.Add(ilAddr);
                    }
                }
            }
            return slice;
        }

        public static void PerformInputSlice(ModuleDefinition module, CSharpDecompiler decompiler, IEnumerable<IMethod> entryPoints)
        {
            MethodPool pool = new MethodPool();
            Console.WriteLine("Computing program slice");
            Dictionary<string, ISet<int>> slice = ComputeSlice(entryPoints, decompiler, pool);
            ISet<string> entryPointSigs = new HashSet<string>();
            foreach (IMethod entryPoint in entryPoints)
            {
                entryPointSigs.Add(MethodSignature(entryPoint));
            }
            foreach (TypeDefinition type in module.Types)
            {
                foreach (MethodDefinition md in type.Methods)
                {
                    string methodSig = MethodSignature(md);
                    if (md.Body == null)
                    {
                        continue;
                    }
                    ILProcessor proc = md.Body.GetILProcessor();
                    if (slice.TryGetValue(methodSig, out ISet<int> methodSlice))
                    {
                        Console.WriteLine("Slicing " + md.FullName);
                        ISet<Instruction> instsToRemove = new HashSet<Instruction>();
                        foreach (Instruction inst in md.Body.Instructions)
                        {
                            if (!methodSlice.Contains(inst.Offset))
                            {
                                instsToRemove.Add(inst);
                            }
                        }
                        bool isEntryPoint = entryPointSigs.Contains(methodSig);
                        foreach (Instruction inst in instsToRemove)
                        {
                            if (isEntryPoint && (inst.OpCode == OpCodes.Ret || inst.OpCode == OpCodes.Leave || inst.OpCode == OpCodes.Leave_S))
                            {
                                continue;
                            }
                            inst.OpCode = OpCodes.Nop;
                        }
                    }
                }
            }
        }

    }

}
