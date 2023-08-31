using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;

namespace UnityActionAnalysis.Tests
{
    using static InputAnalysisTestHelpers;

    [TestClass()]
    public class ReachableMethodsTest
    {
        private static void AssertReachable(CSharpDecompiler decompiler, string fullTypeName, string entryPointMethodName, 
            params string[] methodNames)
        {
            IType type = decompiler.TypeSystem.FindType(new FullTypeName(fullTypeName));
            IMethod entryPoint = type.GetMethods(m => m.Name == entryPointMethodName).First();
            MethodPool pool = new MethodPool();
            ReachableMethods rm = new ReachableMethods(entryPoint, pool);
            ISet<string> expected = new HashSet<string>(methodNames);
            ISet<string> reachable = new HashSet<string>();
            foreach (IMethod method in rm.FindReachableMethods())
            {
                reachable.Add(method.FullName);
            }

            ISet<string> missing = new HashSet<string>();
            ISet<string> unexpected = new HashSet<string>();
            foreach (string name in expected)
            {
                if (!reachable.Contains(name))
                {
                    missing.Add(name);
                }
            }
            foreach (string name in reachable)
            {
                if (!expected.Contains(name))
                {
                    unexpected.Add(name);
                }
            }
            if (missing.Count > 0 || unexpected.Count > 0)
            {
                Assert.Fail("incorrect result for " + fullTypeName + "." + entryPointMethodName 
                    + ": missing=[" + string.Join(",", missing) 
                    + "]; unexpected=[" + string.Join(",", unexpected) + "]");
            }
        }

        [TestMethod()]
        public void TestAnalysis()
        {
            var decompiler = TestHelpers.CreateDecompiler();
            AssertReachable(decompiler, 
                "UnityActionAnalysisTestCases.InputAnalysis.TestC.ProgramC", "Update",
                "UnityActionAnalysisTestCases.InputAnalysis.TestC.ProgramC.Update",
                "UnityActionAnalysisTestCases.InputAnalysis.TestC.ProgramC.f",
                "UnityActionAnalysisTestCases.InputAnalysis.TestC.ProgramC.h",
                "UnityActionAnalysisTestCases.InputAnalysis.TestC.ProgramC.k",
                "UnityEngine.Input.GetAxis",
                "System.Console.WriteLine",
                "System.Single.ToString");
        }
    }

}