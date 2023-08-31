using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ICSharpCode.Decompiler.IL;

namespace UnityActionAnalysis.Tests
{
    using static InputAnalysisTestHelpers;

    [TestClass()]
    public class InputAnalysisTestD
    {

        [TestMethod()]
        public void TestAnalysis()
        {
            InputAnalysis ia = CreateInputAnalysis("UnityActionAnalysisTestCases.InputAnalysis.TestD.ProgramD", "FixedUpdate");
            InputAnalysisResult result = ia.PerformAnalysis();

            InputAnalysisTestCase(ia, result, "f", 
                "IL_0007: stloc S_2(callvirt get_Prop2(ldloc S_1))",
                "IL_000c: stloc V_0(ldloc S_2)",
                "IL_000f: stloc S_3(ldloc V_0)",
                "IL_0010: leave IL_0000 (ldloc S_3)");

            InputAnalysisTestCase(ia, result, "g1", 
                "IL_0006: stloc S_1(call GetAxis(ldloc S_0))",
                "IL_000b: stloc V_0(ldloc S_1)",
                "IL_000e: stloc S_2(ldloc V_0)",
                "IL_000f: leave IL_0000 (ldloc S_2)");

            InputAnalysisTestCase(ia, result, "g2", 
                "IL_0006: stloc S_1(call GetButton(ldloc S_0))",
                "IL_000b: stloc V_0(ldloc S_1)",
                "IL_000e: stloc S_2(ldloc V_0)",
                "IL_000f: leave IL_0000 (ldloc S_2)");

            InputAnalysisTestCase(ia, result, "FixedUpdate", 
                "IL_0013: stloc S_5(call g1(ldloc S_4))",
                "IL_0018: callvirt set_Prop(ldloc S_3, ldloc S_5)",
                "IL_0024: stloc S_8(callvirt get_Prop(ldloc S_7))",
                "IL_0029: call WriteLine(ldloc S_8)",
                "IL_0031: stloc S_10(call f(ldloc S_9))",
                "IL_0036: call WriteLine(ldloc S_10)",
                "IL_003d: stloc S_12(call g2(ldloc S_11))",
                "IL_0042: call WriteLine(ldloc S_12)");
        }

    }

}