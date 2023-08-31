using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.TypeSystem;

namespace UnityActionAnalysis.Tests
{
    using static InputAnalysisTestHelpers;

    [TestClass()]
    public class InputAnalysisTestC
    {

        [TestMethod()]
        public void TestAnalysis()
        {
            InputAnalysis ia = CreateInputAnalysis("UnityActionAnalysisTestCases.InputAnalysis.TestC.ProgramC", "Update");
            InputAnalysisResult result = ia.PerformAnalysis();

            IType programType = ia.EntryPoint.DeclaringType;
            string gSig = AnalysisHelpers.MethodSignature(programType.GetMethods(m => m.Name == "g").First());
            Assert.IsFalse(result.methodResults.ContainsKey(gSig));

            InputAnalysisTestCase(ia, result, "h", 
                "IL_0002: stloc S_1(ldobj System.Int32(delayex.ldflda xf(ldloc S_0)))",
                "IL_0007: stloc S_2(conv.signed i4->r4 (ldloc S_1))",
                "IL_0008: stloc S_3(ldobj System.Single(ldsflda yf))",
                "IL_000d: stloc S_4(binary.add.f4(ldloc S_2, ldloc S_3))",
                "IL_0013: stloc S_6(comp.f4(ldloc S_4 > ldloc S_5))",
                "IL_0015: stloc V_0(ldloc S_6)",
                "IL_0016: stloc S_7(ldloc V_0)",
                "IL_0017: if (comp.i4(ldloc S_7 == ldc.i4 0))",
                "IL_001f: stloc S_9(ldflda zf(ldloc S_8))",
                "IL_0024: stloc S_10(call ToString(ldloc S_9))",
                "IL_0029: stobj System.String(ldsflda qf, ldloc S_10)",
                "IL_0041: stobj System.String(ldsflda qf, ldloc S_12)");
            
            InputAnalysisTestCase(ia, result, "f", 
                "IL_000b: stloc S_2(call GetAxis(ldloc S_1))",
                "IL_0010: stobj System.Single(delayex.ldflda zf(ldloc S_0), ldloc S_2)",
                "IL_001a: stloc S_4(ldobj System.Single(delayex.ldflda zf(ldloc S_3)))",
                "IL_0024: stloc S_6(comp.f4(ldloc S_4 > ldloc S_5))",
                "IL_0026: stloc V_0(ldloc S_6)",
                "IL_0027: stloc S_7(ldloc V_0)",
                "IL_0028: if (comp.i4(ldloc S_7 == ldc.i4 0))");
            
            InputAnalysisTestCase(ia, result, "k", 
                "IL_0001: stloc S_0(ldloc s)",
                "IL_0002: call WriteLine(ldloc S_0)");
            
            InputAnalysisTestCase(ia, result, "Update", 
                "IL_0007: stloc S_2(call GetAxis(ldloc S_1))",
                "IL_0011: stloc S_4(binary.mul.f4(ldloc S_2, ldloc S_3))",
                "IL_0012: stloc S_5(conv f4->i4 (ldloc S_4))",
                "IL_0013: stobj System.Int32(delayex.ldflda xf(ldloc S_0), ldloc S_5)",
                "IL_001d: stloc S_7(call GetAxis(ldloc S_6))",
                "IL_0027: stloc S_9(binary.mul.f4(ldloc S_7, ldloc S_8))",
                "IL_0028: stobj System.Single(ldsflda yf, ldloc S_9)",
                "IL_0044: stloc S_13(ldobj System.Single(delayex.ldflda zf(ldloc S_12)))",
                "IL_004e: stloc S_15(comp.f4(ldloc S_13 > ldloc S_14))",
                "IL_0050: stloc V_0(ldloc S_15)",
                "IL_0051: stloc S_16(ldloc V_0)",
                "IL_0052: if (comp.i4(ldloc S_16 == ldc.i4 0))",
                "IL_006f: stloc S_20(ldobj System.String(ldsflda qf))",
                "IL_0074: call k(ldloc S_19, ldloc S_20)",
                "IL_007b: stloc S_22(ldobj System.Int32(delayex.ldflda xf(ldloc S_21)))",
                "IL_0080: call WriteLine(ldloc S_22)",
                "IL_0086: stloc S_23(ldobj System.Single(ldsflda yf))",
                "IL_008b: call WriteLine(ldloc S_23)",
                "IL_009c: stloc S_25(ldobj System.String(ldsflda qf))",
                "IL_00a1: call WriteLine(ldloc S_25)");
        }

    }

}
