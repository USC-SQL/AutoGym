using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ICSharpCode.Decompiler.IL;

namespace UnityActionAnalysis.Tests
{
    using static InputAnalysisTestHelpers;

    [TestClass()]
    public class LeadsToInputAnalysisTest
    {

        [TestMethod()]
        public void TestAnalysisA()
        {
            InputAnalysis ia = CreateInputAnalysis("UnityActionAnalysisTestCases.LeadsToInputAnalysis.TestA.ProgramA", "Update");
            InputAnalysisResult result = ia.PerformAnalysis();
            LeadsToInputAnalysis ltia = new LeadsToInputAnalysis(ia.EntryPoint, result, ia.pool);
            LeadsToInputAnalysisResult ltResult = ltia.PerformAnalysis();

            InputAnalysisTestCase(ia, result, "Update",
                "IL_001c: stloc S_3(call GetAxis(ldloc S_2))",
                "IL_0021: call WriteLine(ldloc S_3)");

            LeadsToInputAnalysisTestCase(ia, ltResult, "Update", 
                "IL_0000: nop",
                "IL_0001: stloc S_0(ldstr \"A\")",
                "IL_0006: call WriteLine(ldloc S_0)",
                "IL_000b: nop",
                "IL_000c: stloc S_1(ldstr \"B\")",
                "IL_0011: call WriteLine(ldloc S_1)",
                "IL_0016: nop",
                "IL_0017: stloc S_2(ldstr \"Horizontal\")",
                "IL_001c: stloc S_3(call GetAxis(ldloc S_2))",
                "IL_0021: call WriteLine(ldloc S_3)");
        }

        [TestMethod()]
        public void TestAnalysisB()
        {
            InputAnalysis ia = CreateInputAnalysis("UnityActionAnalysisTestCases.LeadsToInputAnalysis.TestB.ProgramB", "Update");
            InputAnalysisResult result = ia.PerformAnalysis();
            LeadsToInputAnalysis ltia = new LeadsToInputAnalysis(ia.EntryPoint, result, ia.pool);
            LeadsToInputAnalysisResult ltResult = ltia.PerformAnalysis();

            InputAnalysisTestCase(ia, result, "Update");
            InputAnalysisTestCase(ia, result, "CheckInput",
                "IL_0006: stloc S_1(call GetAxis(ldloc S_0))",
                "IL_0010: stloc S_3(comp.f4(ldloc S_1 > ldloc S_2))",
                "IL_0012: stloc V_0(ldloc S_3)",
                "IL_0013: stloc S_4(ldloc V_0)",
                "IL_0014: if (comp.i4(ldloc S_4 == ldc.i4 0)) br IL_0023");

            LeadsToInputAnalysisTestCase(ia, ltResult, "Update", 
                "IL_0000: nop",
                "IL_0001: stloc S_0(ldstr \"A\")",
                "IL_0006: call WriteLine(ldloc S_0)",
                "IL_000b: nop",
                "IL_000c: stloc S_1(ldstr \"B\")",
                "IL_0011: call WriteLine(ldloc S_1)",
                "IL_0016: nop",
                "IL_0017: stloc S_2(ldloc this)",
                "IL_0018: call CheckInput(ldloc S_2)");

            LeadsToInputAnalysisTestCase(ia, ltResult, "CheckInput",
                "IL_0000: nop",
                "IL_0001: stloc S_0(ldstr \"Horizontal\")",
                "IL_0006: stloc S_1(call GetAxis(ldloc S_0))",
                "IL_000b: stloc S_2(ldc.f4 0.0)",
                "IL_0010: stloc S_3(comp.f4(ldloc S_1 > ldloc S_2))",
                "IL_0012: stloc V_0(ldloc S_3)",
                "IL_0013: stloc S_4(ldloc V_0)",
                "IL_0014: if (comp.i4(ldloc S_4 == ldc.i4 0)) br IL_0023");
        }

    }

}
