using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnityActionAnalysis.Tests
{

    public static class TransformTestHelpers
    {
        public static string GetOutputAssemblyPath()
        {
            return TestHelpers.GetTestCasesAssemblyPath() + ".tmp.dll";
        }

        public static GameConfiguration MakeOutputAssemblyConfig()
        {
            return new GameConfiguration(
                GetOutputAssemblyPath(),
                null,
                null,
                new List<string>(),
                new HashSet<string>(),
                new HashSet<string>(),
                true);
        }
    }

}