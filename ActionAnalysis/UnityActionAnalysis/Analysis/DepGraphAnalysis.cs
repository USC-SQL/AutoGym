using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.ControlFlow;
using ICSharpCode.Decompiler.CSharp;

namespace UnityActionAnalysis
{

    public class DepGraphAnalysis
    {
        private IMethod entryPoint;
        private MethodPool pool;

        private Dictionary<IMethod, CFG> methods;
        private Dictionary<string, (IMethod, CFG)> methodsBySignature;

        public DepGraphAnalysis(IMethod entryPoint, MethodPool pool)
        {
            this.entryPoint = entryPoint;
            this.pool = pool;
        }

        private bool CFGNodeToDepGraphNode(CFGNode n, IMethod method, out DepGraphNode result)
        {
            if (n is CFGInstructionNode instNode)
            {
                result = new DepGraphInstructionNode((ILInstruction)instNode.NodeObject);
                return true;
            } else if (n.Equals(CFGNode.ENTRY) || n.Equals(CFGNode.START))
            {
                result = new DepGraphMethodNode(method);
                return true;
            } else 
            {
                result = null;
                return false;
            }
        }

        private void addVariableDataDependenceEdges(DepGraph dg)
        {
            ISet<string> varToCheck = new HashSet<string>();
            foreach (var entry in methods)
            {
                IMethod method = entry.Key;
                CFG cfg = entry.Value;
                Dictionary<CFGNode, ISet<RDDefinition>> rdIn = RDDefinition.ComputeReachingDefinitions(cfg);
                foreach (var rdEntry in rdIn)
                {
                    CFGNode node = rdEntry.Key;
                    if (node is CFGInstructionNode instNode)
                    {
                        ILInstruction inst = (ILInstruction)instNode.NodeObject;
                        ISet<RDDefinition> rds = rdEntry.Value;
                        foreach (RDDefinition rd in rds)
                        {
                            varToCheck.Clear();
                            varToCheck.Add(rd.Variable);
                            if (AnalysisHelpers.ExpressionUsesAnyVariable(inst, varToCheck))
                            {
                                CFGNode defNode = rd.DefinitionNode;
                                CFGNode useNode = node;
                                if (CFGNodeToDepGraphNode(defNode, method, out DepGraphNode dgDefNode)
                                && CFGNodeToDepGraphNode(useNode, method, out DepGraphNode dgUseNode))
                                {
                                    dg.AddEdge(new DepGraphDirectedEdge(dgDefNode, dgUseNode, DepGraphEdgeType.DATA_DEPENDENCE_VARIABLE));
                                }
                            }
                        }
                    }
                }
                foreach (CFGNode node in cfg.Nodes)
                {
                    if (node is CFGInstructionNode instNode)
                    {
                        ILInstruction inst = (ILInstruction)instNode.NodeObject;
                        if (AnalysisHelpers.FindInstruction(inst, out LdLoc ldloc))
                        {
                            if (ldloc.Variable.Kind == VariableKind.Parameter)
                            {
                                dg.AddEdge(new DepGraphDirectedEdge( 
                                    new DepGraphMethodNode(method), 
                                    new DepGraphInstructionNode(inst),
                                    DepGraphEdgeType.DATA_DEPENDENCE_VARIABLE));
                            }
                        }
                    }
                }   
            }
        }

        private void addFieldDataDependenceEdges(DepGraph dg)
        {
            foreach (var entry in methods)
            {
                IMethod method = entry.Key;
                CFG cfg = entry.Value;

                foreach (CFGNode node in cfg.Nodes)
                {
                    if (node is CFGInstructionNode instNode)
                    {
                        ILInstruction inst = (ILInstruction)instNode.NodeObject;
                        if (inst is StObj stobj && stobj.Target is IInstructionWithFieldOperand stfop)
                        {
                            DepGraphInstructionNode sourceNode = new DepGraphInstructionNode(inst);
                            DepGraphFieldNode targetNode = new DepGraphFieldNode(stfop.Field);
                            dg.AddEdge(new DepGraphDirectedEdge(sourceNode, targetNode, DepGraphEdgeType.DATA_DEPENDENCE_FIELD));
                        } else if (AnalysisHelpers.FindInstruction(inst, out LdObj ldobj) && ldobj.Target is IInstructionWithFieldOperand ldfop)
                        {
                            DepGraphFieldNode sourceNode = new DepGraphFieldNode(ldfop.Field);
                            DepGraphInstructionNode targetNode = new DepGraphInstructionNode(inst);
                            dg.AddEdge(new DepGraphDirectedEdge(sourceNode, targetNode, DepGraphEdgeType.DATA_DEPENDENCE_FIELD));
                        }
                    }
                }
            }
        }

        private void addBranchControlDependenceEdges(DepGraph dg)
        {
            foreach (var entry in methods)
            {
                IMethod method = entry.Key;
                CFG cfg = entry.Value;
                CFG augCFG = new CFG(cfg);
                augCFG.AddEdge(new CFGDirectedEdge(CFGNode.START, CFGNode.ENTRY));
                augCFG.AddEdge(new CFGDirectedEdge(CFGNode.START, CFGNode.EXIT));
                DomTree postDomTree = DomTree.MakeDomTree(augCFG.Reverse());
                ISet<CFGDirectedEdge> s = new HashSet<CFGDirectedEdge>();
                foreach (CFGDirectedEdge e in augCFG.Edges)
                {
                    CFGNode m = e.Source;
                    CFGNode n = e.Target;
                    if (!postDomTree.DoesDominate(n, m))
                    {
                        s.Add(e);
                    }
                }
                foreach (CFGDirectedEdge e in s)
                {
                    CFGNode m = e.Source;
                    CFGNode n = e.Target;
                    CFGNode l = postDomTree.LowestCommonAncestor(m, n);
                    CFGNode k = n;
                    while (!k.Equals(l))
                    {
                        if (CFGNodeToDepGraphNode(m, method, out DepGraphNode dgm)
                        && CFGNodeToDepGraphNode(k, method, out DepGraphNode dgk)
                        && !dgm.Equals(dgk))
                        {
                            dg.AddEdge(new DepGraphDirectedEdge(dgm, dgk, DepGraphEdgeType.CONTROL_DEPENDENCE_BRANCH));
                        }
                        k = postDomTree.GetParent(k);
                    }
                }
            }
        }

        private void addMethodCallControlDependenceEdges(DepGraph dg)
        {
            foreach (var entry in methods)
            {
                IMethod method = entry.Key;
                CFG cfg = entry.Value;

                foreach (CFGNode node in cfg.Nodes)
                {
                    if (node is CFGInstructionNode instNode)
                    {
                        ILInstruction inst = (ILInstruction)instNode.NodeObject;
                        if (AnalysisHelpers.FindInstruction(inst, out CallInstruction callinst))
                        {
                            IMethod targetMethod = callinst.Method;
                            string targetMethodSig = AnalysisHelpers.MethodSignature(targetMethod);
                            if (methodsBySignature.TryGetValue(targetMethodSig, out (IMethod, CFG) result))
                            {
                                CFG targetCFG = result.Item2;

                                dg.AddEdge(new DepGraphDirectedEdge(
                                    new DepGraphInstructionNode(inst), 
                                    new DepGraphMethodNode(targetMethod),
                                    DepGraphEdgeType.CONTROL_DEPENDENCE_METHOD_CALL));
                                
                                foreach (CFGNode targetNode in targetCFG.Nodes)
                                {
                                    if (targetNode is CFGInstructionNode targetInstNode
                                    && targetInstNode.NodeObject is Leave targetLeave)
                                    {
                                        dg.AddEdge(new DepGraphDirectedEdge(
                                            new DepGraphInstructionNode(targetLeave),
                                            new DepGraphInstructionNode(inst),
                                            DepGraphEdgeType.CONTROL_DEPENDENCE_METHOD_CALL));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public DepGraph PerformAnalysis()
        {
            methods = new Dictionary<IMethod, CFG>();
            methodsBySignature = new Dictionary<string, (IMethod, CFG)>();
            ReachableMethods rm = new ReachableMethods(entryPoint, pool);
            foreach (IMethod m in rm.FindReachableMethods())
            {
                string methodSignature = AnalysisHelpers.MethodSignature(m);
                if (!methodsBySignature.ContainsKey(methodSignature) && AnalysisHelpers.ShouldProcessBody(m, entryPoint))
                {
                    ILFunction func = InstructionPointer.FindInstructionFunction(pool.MethodEntryPoint(m).block);
                    CFG cfg = new CFG(new ControlFlowGraph((BlockContainer)func.Body));
                    methods.Add(m, cfg);
                    methodsBySignature.Add(methodSignature, (m, cfg));
                }
            }

            DepGraph dg = new DepGraph();
            addVariableDataDependenceEdges(dg);
            addFieldDataDependenceEdges(dg);
            addBranchControlDependenceEdges(dg);
            addMethodCallControlDependenceEdges(dg);

            return dg;
        }
    }

}