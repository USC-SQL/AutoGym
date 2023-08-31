using System;
using System.Text;
using System.Collections.Generic;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.ControlFlow;
using ICSharpCode.Decompiler.CSharp;


namespace UnityActionAnalysis
{

    public class RDDefinition
    {
        public string Variable { get; private set; }
        public CFGNode DefinitionNode { get; private set; }

        public RDDefinition(string variable, CFGNode defNode)
        {
            Variable = variable;
            DefinitionNode = defNode;
        }

        public override bool Equals(object obj)
        {
            if (obj is RDDefinition def)
            {
                return def.Variable.Equals(Variable) && def.DefinitionNode.Equals(DefinitionNode);
            } else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return DefinitionNode.GetHashCode() + 31*Variable.GetHashCode();
        }

        public override string ToString()
        {
            return "(" + DefinitionNode.GetLabel() + ", " + Variable + ")";
        }

        public static Dictionary<CFGNode, ISet<RDDefinition>> ComputeReachingDefinitions(CFG cfg)
        {
            Dictionary<CFGNode, ISet<RDDefinition>> rdIn = new Dictionary<CFGNode, ISet<RDDefinition>>();
            Dictionary<CFGNode, ISet<RDDefinition>> rdOut = new Dictionary<CFGNode, ISet<RDDefinition>>();

            foreach (CFGNode node in cfg.Nodes)
            {
                rdIn.Add(node, new HashSet<RDDefinition>());
                rdOut.Add(node, new HashSet<RDDefinition>());
            }

            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (CFGNode node in cfg.Nodes)
                {
                    ISet<RDDefinition> nodeIn = rdIn[node];
                    nodeIn.Clear();
                    foreach (CFGNode pred in cfg.Predecessors(node))
                    {
                        foreach (RDDefinition def in rdOut[pred])
                        {
                            nodeIn.Add(def);
                        }
                    }
                    ISet<RDDefinition> newNodeOut = new HashSet<RDDefinition>(nodeIn);
                    if (node is CFGInstructionNode instNode && instNode.NodeObject is StLoc stloc)
                    {
                        string assignVar = stloc.Variable.Name;

                        // kill
                        foreach (RDDefinition def in nodeIn)
                        {
                            if (def.Variable == assignVar)
                            {
                                newNodeOut.Remove(def);
                            }
                        }

                        // gen
                        newNodeOut.Add(new RDDefinition(assignVar, node));
                    }
                    if (!newNodeOut.SetEquals(rdOut[node]))
                    {
                        changed = true;
                        rdOut[node] = newNodeOut;
                    }
                }
            }

            return rdIn;
        }
    }

}