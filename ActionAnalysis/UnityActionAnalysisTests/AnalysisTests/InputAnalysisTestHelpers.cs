using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnityActionAnalysis.Tests
{
    public static class InputAnalysisTestHelpers
    {
        public static InputAnalysis CreateInputAnalysis(string entryPointClassFullName, string entryPointMethodName)
        {
            var gameConfig = TestHelpers.MakeTestConfiguration();
            string outputPath = TransformTestHelpers.GetOutputAssemblyPath();
            using (var module = ActionAnalysis.LoadAssemblyCecil(gameConfig, gameConfig.assemblyFileName))
            {
                ActionAnalysis.RemoveTryCatch(module, gameConfig);
                module.Write(TransformTestHelpers.GetOutputAssemblyPath());
            }

            var outGameConfig = TransformTestHelpers.MakeOutputAssemblyConfig();
            var decompiler = ActionAnalysis.LoadAssembly(outGameConfig, outGameConfig.assemblyFileName);

            IType program = decompiler.TypeSystem.MainModule.Compilation.FindType(new FullTypeName(entryPointClassFullName));
            IMethod method = program.GetMethods().Where(m => m.Name == entryPointMethodName).First();
            return new InputAnalysis(method, new MethodPool());
        }

        public static ILFunction FetchMethod(IMethod m, MethodPool pool)
        {
            return InstructionPointer.FindInstructionFunction(pool.MethodEntryPoint(m).block);
        }

        public static ILInstruction Entrypoint(ILFunction func)
        {
            BlockContainer bc = (BlockContainer)func.Body;
            return bc.Blocks[0].Instructions[0];
        }

        public static ILInstruction FindInstruction(ILFunction func, string loc, string prefix)
        {
            string suffix = " at " + loc;
            foreach (ILInstruction inst in AnalysisHelpers.Instructions(func))
            {
                var s = inst.ToString();
                if (s.StartsWith(prefix) && s.EndsWith(suffix))
                {
                    return inst;
                }
            }
            throw new System.Exception("could not find instruction '" + prefix + "..." + suffix + "' in " + func.Method.FullName);
        }

        private static void AssertInstructionsSame(ISet<ILInstruction> expected, ISet<ILInstruction> actual, string methodName)
        {
            ISet<ILInstruction> missing = new HashSet<ILInstruction>();
            ISet<ILInstruction> unexpected = new HashSet<ILInstruction>();
            foreach (ILInstruction inst in expected)
            {
                if (!actual.Contains(inst))
                {
                    missing.Add(inst);
                }
            }
            foreach (ILInstruction inst in actual)
            {
                if (!expected.Contains(inst))
                {
                    unexpected.Add(inst);
                }
            }

            if (missing.Count > 0 || unexpected.Count > 0)
            {
                string failMsg = "incorrect result for " + methodName;
                failMsg += "\n\tmissing: [";
                foreach (ILInstruction inst in missing) {
                    failMsg += "\n\t\t" + inst.ToString();
                }
                if (missing.Count > 0) {
                    failMsg += "\n\t";
                }
                failMsg += "]";
                failMsg += "\n\tunexpected: [";
                foreach (ILInstruction inst in unexpected) {
                    failMsg += "\n\t\t" + inst.ToString();
                }
                if (unexpected.Count > 0) {
                    failMsg += "\n\t";
                }
                failMsg += "]";
                Assert.Fail(failMsg);
            }
        }

        public static void InputAnalysisTestCase(InputAnalysis ia, InputAnalysisResult analysisResult, 
            string methodName, params string[] expectedInputDepPoints)
        {
            IType programType = ia.EntryPoint.DeclaringType;
            IMethod method = programType.GetMethods(m => m.Name == methodName).First();
            string methodSig = AnalysisHelpers.MethodSignature(method);
            var func = FetchMethod(method, ia.pool);
            var result = analysisResult.methodResults[methodSig];
            ISet<ILInstruction> expected = new HashSet<ILInstruction>();
            foreach (string pt in expectedInputDepPoints)
            {
                int sep = pt.IndexOf(": ");
                string loc = pt.Substring(0, sep);
                string prefix = pt.Substring(sep+2);
                expected.Add(FindInstruction(func, loc, prefix));
            }
            
            AssertInstructionsSame(expected, result.inputDependentPoints, methodName);
        }

        public static void LeadsToInputAnalysisTestCase(InputAnalysis ia, LeadsToInputAnalysisResult ltResult, string methodName, params string[] expectedLtInputPoints)
        {
            IType programType = ia.EntryPoint.DeclaringType;
            IMethod method = programType.GetMethods(m => m.Name == methodName).First();
            string methodSig = AnalysisHelpers.MethodSignature(method);
            var func = FetchMethod(method, ia.pool);
            var result = ltResult.methodResults[methodSig];

            ISet<ILInstruction> expected = new HashSet<ILInstruction>();
            foreach (string pt in expectedLtInputPoints)
            {
                int sep = pt.IndexOf(": ");
                string loc = pt.Substring(0, sep);
                string prefix = pt.Substring(sep+2);
                expected.Add(FindInstruction(func, loc, prefix));
            }

            AssertInstructionsSame(expected, result.leadsToInputPoints, methodName);
        }
    }
}
