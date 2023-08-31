using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Z3;

namespace UnityActionAnalysis.Tests
{

    [TestClass()]
    public class RemoveTryCatchTest 
    {
        private static void AssertMatch(CSharpDecompiler decompilerBefore, CSharpDecompiler decompilerAfter, string typeName, string methodName)
        {
            IType typeBefore = decompilerBefore.TypeSystem.FindType(new FullTypeName(typeName));
            IType typeAfter = decompilerAfter.TypeSystem.FindType(new FullTypeName(typeName));
            string notryMethodName = methodName + "_notry";
            IMethod methodBefore = typeBefore.GetMethods(m => m.Name == notryMethodName).First();
            IMethod methodAfter = typeAfter.GetMethods(m => m.Name == methodName).First();
            string noTry = decompilerBefore.DecompileAsString(methodBefore.MetadataToken);
            string output = decompilerAfter.DecompileAsString(methodAfter.MetadataToken);
            noTry = noTry.Replace("_notry", "");
            Assert.AreEqual(noTry, output);
        }

        [TestMethod()]
        public void TestRemoveTryCatch()
        {
            GameConfiguration config = TestHelpers.MakeTestConfiguration();
            using (var module = ActionAnalysis.LoadAssemblyCecil(config, config.assemblyFileName))
            {
                ActionAnalysis.RemoveTryCatch(module, config);
                module.Write(TransformTestHelpers.GetOutputAssemblyPath());
            }

            GameConfiguration outConfig = TransformTestHelpers.MakeOutputAssemblyConfig();
            var decompilerBefore = TestHelpers.CreateDecompiler();
            var decompilerAfter = ActionAnalysis.LoadAssembly(outConfig, outConfig.assemblyFileName);

            AssertMatch(decompilerBefore, decompilerAfter, "UnityActionAnalysisTestCases.Transforms.TestA.ProgramA", "f1");
            AssertMatch(decompilerBefore, decompilerAfter, "UnityActionAnalysisTestCases.Transforms.TestA.ProgramA", "f2");
            AssertMatch(decompilerBefore, decompilerAfter, "UnityActionAnalysisTestCases.Transforms.TestA.ProgramA", "f3");
            AssertMatch(decompilerBefore, decompilerAfter, "UnityActionAnalysisTestCases.Transforms.TestA.ProgramA", "f4");
        }
    }

}
