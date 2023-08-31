using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ICSharpCode.Decompiler.IL;

namespace UnityActionAnalysis.Tests
{
    using static InputAnalysisTestHelpers;

    [TestClass()]
    public class InputAnalysisTestB
    {

        [TestMethod()]
        public void TestAnalysis()
        {
            InputAnalysis ia = CreateInputAnalysis("UnityActionAnalysisTestCases.InputAnalysis.TestB.ProgramB", "Update");
            InputAnalysisResult result = ia.PerformAnalysis();
 
            InputAnalysisTestCase(ia, result, "f");

            InputAnalysisTestCase(ia, result, "g",
                    "IL_0001: stloc S_0(ldloc b)",
                    "IL_0002: call WriteLine(ldloc S_0)");

            InputAnalysisTestCase(ia, result, "ProcessMouseCoords",
                    "IL_000b: stloc S_2(ldloc mouseCoords)",
                    "IL_000c: stloc S_3(ldobj System.Single(delayex.ldflda x(addressof UnityEngine.Vector3(ldloc S_2))))",
                    "IL_0016: stloc S_5(binary.div.signed.f4(ldloc S_3, ldloc S_4))",
                    "IL_0017: stobj System.Single(delayex.ldflda mouseX(ldloc S_1), ldloc S_5)",
                    "IL_001e: stloc S_7(ldloc mouseCoords)",
                    "IL_001f: stloc S_8(ldobj System.Single(delayex.ldflda y(addressof UnityEngine.Vector3(ldloc S_7))))",
                    "IL_0029: stloc S_10(binary.div.signed.f4(ldloc S_8, ldloc S_9))",
                    "IL_002a: stobj System.Single(delayex.ldflda mouseY(ldloc S_6), ldloc S_10)");

            InputAnalysisTestCase(ia, result, "Update",
                    "IL_0001: stloc S_0(call get_mousePosition())",
                    "IL_0006: stloc V_0(ldloc S_0)",
                    "IL_0008: stloc S_2(ldloc V_0)",
                    "IL_0009: stloc S_3(call ProcessMouseCoords(ldloc S_1, ldloc S_2))",
                    "IL_0010: stloc S_5(ldobj System.Single(delayex.ldflda mouseX(addressof UnityActionAnalysisTestCases.InputAnalysis.TestB.MouseData(ldloc S_4))))",
                    "IL_001a: stloc S_7(binary.add.f4(ldloc S_5, ldloc S_6))",
                    "IL_001b: stloc V_2(ldloc S_7)",
                    "IL_001d: stloc S_9(ldobj System.Single(delayex.ldflda mouseY(addressof UnityActionAnalysisTestCases.InputAnalysis.TestB.MouseData(ldloc S_8))))",
                    "IL_0027: stloc S_11(binary.add.f4(ldloc S_9, ldloc S_10))",
                    "IL_0028: stloc V_3(ldloc S_11)",
                    "IL_003d: stloc S_17(ldloc V_2)",
                    "IL_003e: call WriteLine(ldloc S_17)",
                    "IL_0044: stloc S_18(ldloc V_3)",
                    "IL_0045: call WriteLine(ldloc S_18)",
                    "IL_005d: stloc S_23(call GetButton(ldloc S_22))",
                    "IL_0062: stloc S_24(call g(ldloc S_21, ldloc S_23))");
        }

    }

}
