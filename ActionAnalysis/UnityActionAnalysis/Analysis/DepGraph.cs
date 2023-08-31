using System;
using System.Text;
using System.Collections.Generic;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.CSharp;

namespace UnityActionAnalysis
{

    public abstract class DepGraphNode 
    {
        public object NodeObject { get; private set; }

        public DepGraphNode(object nodeObject)
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
            if (obj is DepGraphNode other)
            {
                return ComputeHash() == other.ComputeHash();
            } else
            {
                return false;
            }
        }
    }

    public class DepGraphMethodNode : DepGraphNode
    {
        public DepGraphMethodNode(IMethod method) :
            base(method)
        {
        }

        protected override int ComputeHash()
        {
            string signature = AnalysisHelpers.MethodSignature((IMethod)NodeObject);
            return signature.GetHashCode();
        }

        public override string GetLabel()
        {
            IMethod method = (IMethod)NodeObject;
            return method.FullName;
        }
    }

    public class DepGraphInstructionNode : DepGraphNode
    {
        public DepGraphInstructionNode(ILInstruction inst) :
            base(inst)
        {
        }

        public static int ComputeInstructionHash(ILInstruction inst)
        {
            IMethod method = InstructionPointer.FindInstructionFunction(inst).Method;
            string methodSig = AnalysisHelpers.MethodSignature(method);
            string instStr = inst.ToString();
            int methodHash = methodSig.GetHashCode();
            int instHash = instStr.GetHashCode();
            return methodHash*31 + instHash;
        }

        protected override int ComputeHash()
        {
            return ComputeInstructionHash((ILInstruction)NodeObject);
        }

        public override string GetLabel()
        {
            ILInstruction inst = (ILInstruction)NodeObject;
            string methodName = InstructionPointer.FindInstructionFunction(inst).Method.Name;
            return methodName + ": " + inst.ToString();
        }
    }

    public class DepGraphFieldNode : DepGraphNode
    {
        public DepGraphFieldNode(IField field) :
            base(field)
        {
        }

        protected override int ComputeHash()
        {
            string signature = AnalysisHelpers.FieldSignature((IField)NodeObject);
            return signature.GetHashCode();
        }

        public override string GetLabel()
        {
            IField field = (IField)NodeObject;
            return field.FullName;
        }
    }

    public enum DepGraphEdgeType
    {
        CONTROL_DEPENDENCE_BRANCH,
        CONTROL_DEPENDENCE_METHOD_CALL,
        DATA_DEPENDENCE_VARIABLE,
        DATA_DEPENDENCE_FIELD
    }

    public class DepGraphDirectedEdge 
    {
        public DepGraphNode Source { get; private set; }
        public DepGraphNode Target { get; private set; }
        public DepGraphEdgeType EdgeType { get; private set; }

        public DepGraphDirectedEdge(DepGraphNode source, DepGraphNode target, DepGraphEdgeType edgeType)
        {
            Source = source;
            Target = target;
            EdgeType = edgeType;
        }

        public override bool Equals(object obj)
        {
            if (obj is DepGraphDirectedEdge other)
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
            int edgeTypeHash = EdgeType.GetHashCode();
            return (sourceHash*31 + targetHash)*31 + edgeTypeHash;
        }
    }

    public class DepGraph 
    {
        private HashSet<DepGraphDirectedEdge> edges;
        private Dictionary<DepGraphNode, HashSet<DepGraphDirectedEdge>> incomingEdges;

        public IEnumerable<DepGraphNode> Nodes { get => incomingEdges.Keys; }
        public IReadOnlySet<DepGraphDirectedEdge> Edges { get => edges; }

        public DepGraph()
        {
            edges = new HashSet<DepGraphDirectedEdge>();
            incomingEdges = new Dictionary<DepGraphNode, HashSet<DepGraphDirectedEdge>>();
        }

        public IEnumerable<DepGraphDirectedEdge> GetIncomingEdges(DepGraphNode node)
        {
            if (incomingEdges.TryGetValue(node, out HashSet<DepGraphDirectedEdge> incoming))
            {
                foreach (DepGraphDirectedEdge edge in incoming)
                {
                    yield return edge;
                }
            }
            yield break;
        }

        public void AddEdge(DepGraphDirectedEdge edge)
        {
            edges.Add(edge);
            HashSet<DepGraphDirectedEdge> incoming;
            if (!incomingEdges.TryGetValue(edge.Target, out incoming))
            {
                incoming = new HashSet<DepGraphDirectedEdge>();
                incomingEdges.Add(edge.Target, incoming);
            }
            incoming.Add(edge);
        }

        public string ToDotty()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("digraph depgraph {\n");
            foreach (DepGraphDirectedEdge edge in edges)
            {
                string color;
                switch (edge.EdgeType)
                {
                    case DepGraphEdgeType.CONTROL_DEPENDENCE_BRANCH:
                        color = "green";
                        break;
                    case DepGraphEdgeType.CONTROL_DEPENDENCE_METHOD_CALL:
                        color = "blue";
                        break;
                    case DepGraphEdgeType.DATA_DEPENDENCE_VARIABLE:
                        color = "red";
                        break;
                    case DepGraphEdgeType.DATA_DEPENDENCE_FIELD:
                        color = "orange";
                        break;
                    default:
                        throw new Exception("unexpected edge type " + edge.EdgeType);
                }
                
                string sourceLabel = edge.Source.GetLabel();
                string targetLabel = edge.Target.GetLabel();
                sb.Append("\"");
                sb.Append(sourceLabel.Replace("\"", "\\\""));
                sb.Append("\"");
                sb.Append("->");
                sb.Append("\"");
                sb.Append(targetLabel.Replace("\"", "\\\""));
                sb.Append("\" [color=\"");
                sb.Append(color);
                sb.Append("\"]\n");
            }
            sb.Append("}\n");
            return sb.ToString();
        }
    }

}