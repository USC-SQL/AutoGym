using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.ControlFlow;
using ICSharpCode.Decompiler.CSharp;

namespace UnityActionAnalysis
{
    /* This file defines an instruction-level control-flow graph which can also 
       be easily reversed for use in computing control dependencies. */

    public abstract class CFGNode
    {
        public static readonly CFGNode ENTRY = new CFGSpecialNode("entry");
        public static readonly CFGNode EXIT = new CFGSpecialNode("exit");
        public static readonly CFGNode START = new CFGSpecialNode("start");

        public object NodeObject { get; private set; }

        public CFGNode(object nodeObject)
        {
            NodeObject = nodeObject;
        }

        protected abstract int ComputeHash();
        
        public abstract string GetLabel();

        public override int GetHashCode()
        {
            return ComputeHash();
        }

        public override bool Equals(object obj)
        {
            if (obj is CFGNode other)
            {
                return ComputeHash() == other.ComputeHash();
            } else
            {
                return false;
            }
        }

        public static ISet<CFGNode> IntersectSets(IEnumerable<ISet<CFGNode>> sets)
        {
            ISet<CFGNode> result = new HashSet<CFGNode>();
            foreach (ISet<CFGNode> s in sets)
            {
                foreach (CFGNode n in s)
                {
                    bool containedInAll = true;
                    foreach (ISet<CFGNode> t in sets)
                    {
                        if (!t.Contains(n))
                        {
                            containedInAll = false;
                            break;
                        }
                    }
                    if (containedInAll)
                    {
                        result.Add(n);
                    }
                }
            }
            return result;
        }
    }   

    public class CFGInstructionNode : CFGNode
    {
        public CFGInstructionNode(ILInstruction inst) :
            base(inst)
        {
        }

        protected override int ComputeHash()
        {
            return DepGraphInstructionNode.ComputeInstructionHash((ILInstruction)NodeObject);
        }

        public override string GetLabel()
        {
            return ((ILInstruction)NodeObject).ToString();
        }
    }

    public class CFGSpecialNode : CFGNode
    {
        public CFGSpecialNode(string id) :
            base(id)
        {   
        }

        protected override int ComputeHash()
        {
            return ((string)NodeObject).GetHashCode();
        }

        public override string GetLabel()
        {
            return (string)NodeObject;
        }
    }

    public class CFGDirectedEdge
    {
        public CFGNode Source { get; private set; }
        public CFGNode Target { get; private set; }

        public CFGDirectedEdge(CFGNode source, CFGNode target)
        {
            Source = source;
            Target = target;
        }

        public override bool Equals(object obj)
        {
            if (obj is CFGDirectedEdge other)
            {
                return other.Source.Equals(Source) && other.Target.Equals(Target);
            } else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            int sourceHash = Source.GetHashCode();
            int targetHash = Target.GetHashCode();
            return sourceHash*31 + targetHash;
        }
    }

    public class CFG 
    {
        private HashSet<CFGNode> nodes;
        private HashSet<CFGDirectedEdge> edges;
        private Dictionary<CFGNode, HashSet<CFGNode>> successors;
        private Dictionary<CFGNode, HashSet<CFGNode>> predecessors;

        public IReadOnlySet<CFGNode> Nodes { get => nodes; }

        public IReadOnlySet<CFGDirectedEdge> Edges { get => edges; }


        public CFG() {
            nodes = new HashSet<CFGNode>();
            edges = new HashSet<CFGDirectedEdge>();
            successors = new Dictionary<CFGNode, HashSet<CFGNode>>();
            predecessors = new Dictionary<CFGNode, HashSet<CFGNode>>();
        }

        public CFG(CFG source) :
            this()
        {
            foreach (CFGDirectedEdge edge in source.Edges)
            {
                AddEdge(edge);
            }
        }

        public CFG(ControlFlowGraph cfg) :
            this()
        {
            ILFunction func = InstructionPointer.FindInstructionFunction(cfg.Container);
            if (AnalysisHelpers.Instructions(func).Any())
            {
                ILInstruction firstInst = AnalysisHelpers.Instructions(func).First();
                AddEdge(new CFGDirectedEdge(CFGNode.ENTRY, new CFGInstructionNode(firstInst)));
                foreach (ILInstruction inst in AnalysisHelpers.Instructions(func))
                {
                    CFGNode sourceNode = new CFGInstructionNode(inst);
                    foreach (ILInstruction succ in AnalysisHelpers.Successors(inst, cfg))
                    {
                        CFGNode targetNode = new CFGInstructionNode(succ);
                        if (!sourceNode.Equals(targetNode))
                        {
                            AddEdge(new CFGDirectedEdge(sourceNode, targetNode));
                        }
                    }
                    if (inst is Leave || inst is Throw)
                    {
                        AddEdge(new CFGDirectedEdge(sourceNode, CFGNode.EXIT));
                    }
                }
            } else
            {
                AddEdge(new CFGDirectedEdge(CFGNode.ENTRY, CFGNode.EXIT));
            }
        }

        public IEnumerator<CFGNode> Successors(CFGNode n)
        {
            foreach (CFGNode succ in successors[n])
            {
                yield return succ;
            }
        }

        public IEnumerable<CFGNode> Predecessors(CFGNode n)
        {
            foreach (CFGNode pred in predecessors[n])
            {
                yield return pred;
            }
        }

        public CFGNode FindHead()
        {
            foreach (CFGNode n in Nodes)
            {
                if (!Predecessors(n).Any())
                {
                    return n;
                }
            }
            throw new Exception("did not find head");
        }

        public CFG Reverse()
        {
            CFG result = new CFG();
            foreach (CFGDirectedEdge edge in edges)
            {
                result.AddEdge(new CFGDirectedEdge(edge.Target, edge.Source));
            }
            return result;
        }

        private void AddNode(CFGNode node)
        {
            nodes.Add(node);
            if (!successors.ContainsKey(node))
            {
                successors.Add(node, new HashSet<CFGNode>());
            }
            if (!predecessors.ContainsKey(node))
            {
                predecessors.Add(node, new HashSet<CFGNode>());
            }
        }

        public void AddEdge(CFGDirectedEdge e)
        {
            AddNode(e.Source);
            AddNode(e.Target);
            edges.Add(e);
            successors[e.Source].Add(e.Target);
            predecessors[e.Target].Add(e.Source);
        }

        public string ToDotty()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("digraph cfg {\n");
            foreach (CFGDirectedEdge edge in edges)
            {
                sb.Append("\t\"");
                sb.Append(edge.Source.GetLabel().Replace("\"", "\\\""));
                sb.Append("\" -> \"");
                sb.Append(edge.Target.GetLabel().Replace("\"", "\\\""));
                sb.Append("\"\n");
            }
            sb.Append("}\n");
            return sb.ToString();
        }
    }
}