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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ICSharpCode.Decompiler.IL;
using Microsoft.Z3;

namespace UnityActionAnalysis.Tests
{
    public class TestHelpers
    {
        public static string GetTestCasesAssemblyPath()
        {
            return @"../../../../../UnityActionAnalysisTestCases/bin/x64/Debug/net6.0/UnityActionAnalysisTestCases.dll";
        }


        public static GameConfiguration MakeTestConfiguration()
        {
            return new GameConfiguration(
                GetTestCasesAssemblyPath(), 
                null,
                null,
                new List<string>(),
                new HashSet<string>(),
                new HashSet<string>(),
                true);
        }

        public static CSharpDecompiler CreateDecompiler()
        {
            GameConfiguration config = MakeTestConfiguration();
            return ActionAnalysis.LoadAssembly(config, config.assemblyFileName);
        }
    }
}
