using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; 
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.CSharp;

namespace UnityActionAnalysis.Tests
{
    class DepGraphTestHelper
    {
        private MethodPool pool;

        public DepGraphTestHelper(MethodPool pool)
        {
            this.pool = pool;
        }

        public DepGraphNode Instruction(IMethod method, string instAddr)
        {
            ILFunction func = InstructionPointer.FindInstructionFunction(pool.MethodEntryPoint(method).block);
            ILInstruction inst = InputAnalysisTestHelpers.FindInstruction(func, instAddr, "");
            return new DepGraphInstructionNode(inst);
        }

        public DepGraphNode MethodEntry(IMethod method)
        {
            return new DepGraphMethodNode(method);
        }

        public DepGraphNode Field(IField field)
        {
            return new DepGraphFieldNode(field);
        }
    }

    [TestClass()]
    public class DepGraphTests
    {
        private static DepGraphDirectedEdge DDVarEdge(DepGraphNode source, DepGraphNode target)
        {
            return new DepGraphDirectedEdge(source, target, DepGraphEdgeType.DATA_DEPENDENCE_VARIABLE);
        }

        private static DepGraphDirectedEdge DDFieldEdge(DepGraphNode source, DepGraphNode target)
        {
            return new DepGraphDirectedEdge(source, target, DepGraphEdgeType.DATA_DEPENDENCE_FIELD);
        }

        private static DepGraphDirectedEdge CDBranchEdge(DepGraphNode source, DepGraphNode target)
        {
            return new DepGraphDirectedEdge(source, target, DepGraphEdgeType.CONTROL_DEPENDENCE_BRANCH);
        }

        private static DepGraphDirectedEdge CDCallEdge(DepGraphNode source, DepGraphNode target)
        {
            return new DepGraphDirectedEdge(source, target, DepGraphEdgeType.CONTROL_DEPENDENCE_METHOD_CALL);
        }

        private static void AssertEdgesEqual(IReadOnlySet<DepGraphDirectedEdge> expected, IReadOnlySet<DepGraphDirectedEdge> actual)
        {
            if (!expected.SetEquals(actual))
            {
                ISet<DepGraphDirectedEdge> missing = new HashSet<DepGraphDirectedEdge>();
                ISet<DepGraphDirectedEdge> unexpected = new HashSet<DepGraphDirectedEdge>();
                foreach (var edge in expected)
                {
                    if (!actual.Contains(edge))
                    {
                        missing.Add(edge);
                    }
                }
                foreach (var edge in actual)
                {
                    if (!expected.Contains(edge))
                    {
                        unexpected.Add(edge);
                    }
                }
                StringBuilder sb = new StringBuilder();
                sb.Append("dependence graph mismatch:\n");
                if (missing.Count > 0)
                {
                    sb.Append("\tmissing edges:\n");
                    foreach (var edgeType in Enum.GetValues<DepGraphEdgeType>())
                    {
                        ISet<DepGraphDirectedEdge> edges = new HashSet<DepGraphDirectedEdge>(missing.Where(e => e.EdgeType == edgeType));
                        if (edges.Count > 0)
                        {
                            sb.Append("\t\t" + edgeType + ":\n");
                            foreach (var e in edges)
                            {
                                sb.Append("\t\t\t" + e.Source.GetLabel() + " -> " + e.Target.GetLabel() + "\n");
                            }
                        }
                    }
                }
                if (unexpected.Count > 0) 
                {
                    sb.Append("\tunexpected edges:\n");
                    foreach (var edgeType in Enum.GetValues<DepGraphEdgeType>())
                    {
                        ISet<DepGraphDirectedEdge> edges = new HashSet<DepGraphDirectedEdge>(unexpected.Where(e => e.EdgeType == edgeType));
                        if (edges.Count > 0)
                        {
                            sb.Append("\t\t" + edgeType + ":\n");
                            foreach (var e in edges)
                            {
                                sb.Append("\t\t\t" + e.Source.GetLabel() + " -> " + e.Target.GetLabel() + "\n");
                            }
                        }
                    }
                }
                Assert.Fail(sb.ToString());
            }
        }

        [TestMethod()]
        public void TestDepGraphA()
        {
            MethodPool pool = new MethodPool();
            CSharpDecompiler decompiler = TestHelpers.CreateDecompiler();
            IType programA = decompiler.TypeSystem.FindType(new FullTypeName("UnityActionAnalysisTestCases.DependenceGraph.TestA.ProgramA"));
            IType myObject = decompiler.TypeSystem.FindType(new FullTypeName("UnityActionAnalysisTestCases.DependenceGraph.TestA.MyObject"));
            IMethod myObjectCtor = myObject.GetConstructors().First();
            IMethod myObjectGetValue = myObject.GetMethods(myObject => myObject.Name == "GetValue").First();
            IField myObjectValue = myObject.GetFields(f => f.Name == "value").First();
            IMethod update = programA.GetMethods(m => m.Name == "Update").First();
            IMethod f = programA.GetMethods(m => m.Name == "f").First();

            IField val = programA.GetFields(f => f.Name == "val").First();
            DepGraphTestHelper h = new DepGraphTestHelper(pool);
            DepGraphAnalysis dga = new DepGraphAnalysis(update, pool);
            DepGraph dg = dga.PerformAnalysis();

            HashSet<DepGraphDirectedEdge> expectedEdges = new HashSet<DepGraphDirectedEdge>();
            expectedEdges.Add(DDFieldEdge(h.Field(myObjectValue), h.Instruction(myObjectGetValue, "IL_0002")));
            expectedEdges.Add(DDFieldEdge(h.Instruction(myObjectCtor, "IL_000a"), h.Field(myObjectValue)));
            expectedEdges.Add(DDFieldEdge(h.Instruction(update, "IL_002f"), h.Field(val)));
            expectedEdges.Add(DDFieldEdge(h.Field(val), h.Instruction(f, "IL_000e")));

            expectedEdges.Add(CDCallEdge(h.Instruction(update, "IL_0021"), h.MethodEntry(myObjectCtor)));
            expectedEdges.Add(CDCallEdge(h.Instruction(myObjectCtor, "IL_000f"), h.Instruction(update, "IL_0021")));
            expectedEdges.Add(CDCallEdge(h.Instruction(update, "IL_0036"), h.MethodEntry(f)));
            expectedEdges.Add(CDCallEdge(h.Instruction(f, "IL_0026"), h.Instruction(update, "IL_0036")));
            expectedEdges.Add(CDCallEdge(h.Instruction(f, "IL_0002"), h.MethodEntry(myObjectGetValue)));
            expectedEdges.Add(CDCallEdge(h.Instruction(myObjectGetValue, "IL_000b"), h.Instruction(f, "IL_0002")));

            expectedEdges.Add(DDVarEdge(h.MethodEntry(myObjectCtor), h.Instruction(myObjectCtor, "IL_0000")));
            expectedEdges.Add(DDVarEdge(h.MethodEntry(myObjectCtor), h.Instruction(myObjectCtor, "IL_0008")));
            expectedEdges.Add(DDVarEdge(h.MethodEntry(myObjectCtor), h.Instruction(myObjectCtor, "IL_0009")));
            expectedEdges.Add(DDVarEdge(h.Instruction(myObjectCtor, "IL_0000"), h.Instruction(myObjectCtor, "IL_0001")));
            expectedEdges.Add(DDVarEdge(h.Instruction(myObjectCtor, "IL_0008"), h.Instruction(myObjectCtor, "IL_000a")));
            expectedEdges.Add(DDVarEdge(h.Instruction(myObjectCtor, "IL_0009"), h.Instruction(myObjectCtor, "IL_000a")));

            expectedEdges.Add(DDVarEdge(h.MethodEntry(myObjectGetValue), h.Instruction(myObjectGetValue, "IL_0001")));
            expectedEdges.Add(DDVarEdge(h.Instruction(myObjectGetValue, "IL_0001"), h.Instruction(myObjectGetValue, "IL_0002")));
            expectedEdges.Add(DDVarEdge(h.Instruction(myObjectGetValue, "IL_0002"), h.Instruction(myObjectGetValue, "IL_0007")));
            expectedEdges.Add(DDVarEdge(h.Instruction(myObjectGetValue, "IL_0007"), h.Instruction(myObjectGetValue, "IL_000a")));
            expectedEdges.Add(DDVarEdge(h.Instruction(myObjectGetValue, "IL_000a"), h.Instruction(myObjectGetValue, "IL_000b")));

            expectedEdges.Add(DDVarEdge(h.MethodEntry(update), h.Instruction(update, "IL_0034")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0001"), h.Instruction(update, "IL_0006")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0006"), h.Instruction(update, "IL_000b")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_000b"), h.Instruction(update, "IL_000c")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_000b"), h.Instruction(update, "IL_0027")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_000c"), h.Instruction(update, "IL_000d")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_000d"), h.Instruction(update, "IL_0012")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0012"), h.Instruction(update, "IL_0013")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0012"), h.Instruction(update, "IL_0020")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0013"), h.Instruction(update, "IL_0019")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0014"), h.Instruction(update, "IL_0019")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0019"), h.Instruction(update, "IL_001b")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_001b"), h.Instruction(update, "IL_001c")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_001c"), h.Instruction(update, "IL_001d")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0020"), h.Instruction(update, "IL_0021")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0021"), h.Instruction(update, "IL_0026")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0026"), h.Instruction(update, "IL_0035")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0027"), h.Instruction(update, "IL_002d")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0028"), h.Instruction(update, "IL_002d")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_002d"), h.Instruction(update, "IL_002e")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_002e"), h.Instruction(update, "IL_002f")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0034"), h.Instruction(update, "IL_0036")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0035"), h.Instruction(update, "IL_0036")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_0036"), h.Instruction(update, "IL_003b")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_003b"), h.Instruction(update, "IL_003d")));
            expectedEdges.Add(DDVarEdge(h.Instruction(update, "IL_003d"), h.Instruction(update, "IL_003f")));

            expectedEdges.Add(DDVarEdge(h.MethodEntry(f), h.Instruction(f, "IL_0001")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_0001"), h.Instruction(f, "IL_0002")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_0002"), h.Instruction(f, "IL_000c")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_0007"), h.Instruction(f, "IL_000c")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_000c"), h.Instruction(f, "IL_000d")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_000d"), h.Instruction(f, "IL_001b")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_000d"), h.Instruction(f, "IL_0020")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_000e"), h.Instruction(f, "IL_0014")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_0013"), h.Instruction(f, "IL_0014")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_0014"), h.Instruction(f, "IL_0016")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_0016"), h.Instruction(f, "IL_0017")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_0017"), h.Instruction(f, "IL_0018")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_001b"), h.Instruction(f, "IL_001c")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_001c"), h.Instruction(f, "IL_0025")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_0020"), h.Instruction(f, "IL_0021")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_0021"), h.Instruction(f, "IL_0022")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_0022"), h.Instruction(f, "IL_0025")));
            expectedEdges.Add(DDVarEdge(h.Instruction(f, "IL_0025"), h.Instruction(f, "IL_0026")));

            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectCtor), h.Instruction(myObjectCtor, "IL_0000")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectCtor), h.Instruction(myObjectCtor, "IL_0001")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectCtor), h.Instruction(myObjectCtor, "IL_0006")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectCtor), h.Instruction(myObjectCtor, "IL_0007")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectCtor), h.Instruction(myObjectCtor, "IL_0008")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectCtor), h.Instruction(myObjectCtor, "IL_0009")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectCtor), h.Instruction(myObjectCtor, "IL_000a")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectCtor), h.Instruction(myObjectCtor, "IL_000f")));

            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectGetValue), h.Instruction(myObjectGetValue, "IL_0000")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectGetValue), h.Instruction(myObjectGetValue, "IL_0001")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectGetValue), h.Instruction(myObjectGetValue, "IL_0002")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectGetValue), h.Instruction(myObjectGetValue, "IL_0007")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectGetValue), h.Instruction(myObjectGetValue, "IL_0008")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectGetValue), h.Instruction(myObjectGetValue, "IL_000a")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(myObjectGetValue), h.Instruction(myObjectGetValue, "IL_000b")));

            expectedEdges.Add(CDBranchEdge(h.MethodEntry(update), h.Instruction(update, "IL_0000")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(update), h.Instruction(update, "IL_0001")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(update), h.Instruction(update, "IL_0006")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(update), h.Instruction(update, "IL_000b")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(update), h.Instruction(update, "IL_000c")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(update), h.Instruction(update, "IL_000d")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(update), h.Instruction(update, "IL_0012")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(update), h.Instruction(update, "IL_0013")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(update), h.Instruction(update, "IL_0014")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(update), h.Instruction(update, "IL_0019")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(update), h.Instruction(update, "IL_001b")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(update), h.Instruction(update, "IL_001c")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(update), h.Instruction(update, "IL_001d")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_001f")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_0020")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_0021")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_0026")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_0027")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_0028")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_002d")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_002e")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_002f")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_0034")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_0035")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_0036")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_003b")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_003d")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_003f")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_0044")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(update, "IL_001d"), h.Instruction(update, "IL_0045")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(update), h.Instruction(update, "IL_0046")));

            expectedEdges.Add(CDBranchEdge(h.MethodEntry(f), h.Instruction(f, "IL_0000")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(f), h.Instruction(f, "IL_0001")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(f), h.Instruction(f, "IL_0002")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(f), h.Instruction(f, "IL_0007")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(f), h.Instruction(f, "IL_000c")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(f), h.Instruction(f, "IL_000d")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(f), h.Instruction(f, "IL_000e")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(f), h.Instruction(f, "IL_0013")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(f), h.Instruction(f, "IL_0014")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(f), h.Instruction(f, "IL_0016")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(f), h.Instruction(f, "IL_0017")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(f), h.Instruction(f, "IL_0018")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(f, "IL_0018"), h.Instruction(f, "IL_001a")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(f, "IL_0018"), h.Instruction(f, "IL_001b")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(f, "IL_0018"), h.Instruction(f, "IL_001c")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(f, "IL_0018"), h.Instruction(f, "IL_001d")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(f, "IL_0018"), h.Instruction(f, "IL_001f")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(f, "IL_0018"), h.Instruction(f, "IL_0020")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(f, "IL_0018"), h.Instruction(f, "IL_0021")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(f, "IL_0018"), h.Instruction(f, "IL_0022")));
            expectedEdges.Add(CDBranchEdge(h.Instruction(f, "IL_0018"), h.Instruction(f, "IL_0023")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(f), h.Instruction(f, "IL_0025")));
            expectedEdges.Add(CDBranchEdge(h.MethodEntry(f), h.Instruction(f, "IL_0026")));

            AssertEdgesEqual(expectedEdges, dg.Edges);
        }
    }
}