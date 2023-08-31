using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ICSharpCode.Decompiler.IL;

namespace UnityActionAnalysis.Tests
{
    using static InputAnalysisTestHelpers;

    [TestClass()]
    public class InputAnalysisTestA
    {

        [TestMethod()]
        public void TestAnalysis()
        {
            InputAnalysis ia = CreateInputAnalysis("UnityActionAnalysisTestCases.InputAnalysis.TestA.ProgramA", "Update");
            InputAnalysisResult result = ia.PerformAnalysis();

            InputAnalysisTestCase(ia, result, "f",
                    "IL_0001: stloc S_0(ldloc x)",
                    "IL_0002: call WriteLine(ldloc S_0)",
                    "IL_0008: stloc S_1(ldloc x)",
                    "IL_000e: stloc S_3(comp.f4(ldloc S_1 < ldloc S_2))",
                    "IL_0010: stloc V_0(ldloc S_3)",
                    "IL_0011: stloc S_4(ldloc V_0)",
                    "IL_0012: if (comp.i4(ldloc S_4 == ldc.i4 0))");

            InputAnalysisTestCase(ia, result, "g",
                    "IL_0001: stloc S_0(ldloc x)",
                    "IL_0007: stloc S_2(comp.f4(ldloc S_0 > ldloc S_1))",
                    "IL_0009: stloc V_0(ldloc S_2)",
                    "IL_000a: stloc S_3(ldloc V_0)",
                    "IL_000b: if (comp.i4(ldloc S_3 == ldc.i4 0))");

            InputAnalysisTestCase(ia, result, "Update",
                    "IL_0006: stloc S_1(call GetButton(ldloc S_0))",
                    "IL_000b: stloc V_0(ldloc S_1)",
                    "IL_000c: stloc S_2(ldloc V_0)",
                    "IL_000d: stloc V_3(ldloc S_2)",
                    "IL_000e: stloc S_3(ldloc V_3)",
                    "IL_000f: if (comp.i4(ldloc S_3 == ldc.i4 0))",
                    "IL_0047: stloc S_15(call GetAxis(ldloc S_14))",
                    "IL_004c: stloc V_1(ldloc S_15)",
                    "IL_0059: stloc S_18(ldloc V_1)",
                    "IL_005a: call f(ldloc S_17, ldloc S_18)",
                    "IL_0061: stloc S_20(ldloc V_1)",
                    "IL_0062: stloc S_21(call g(ldloc S_19, ldloc S_20))");
        }

    }

}
