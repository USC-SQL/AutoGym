using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Z3;
using ICSharpCode.Decompiler.TypeSystem;

namespace UnityActionAnalysis.Tests
{
    [TestClass()]
    public class TestMisc
    {
        [TestMethod()]
        public void TestSymcallIdFromVarName()
        {
            bool r1 = Helpers.GetSymcallIdFromVarName("symcall:1", out int s1);
            bool r2 = Helpers.GetSymcallIdFromVarName("symcall:33", out int s2);
            bool r3 = Helpers.GetSymcallIdFromVarName("symcall:5:instancefield:x", out int s3);
            bool r4 = Helpers.GetSymcallIdFromVarName("symcall:983:instancefield:y:instancefield:q", out int s4);
            bool r5 = Helpers.GetSymcallIdFromVarName("somevar", out int s5);
            Assert.AreEqual(r1, true);
            Assert.AreEqual(r2, true);
            Assert.AreEqual(r3, true);
            Assert.AreEqual(r4, true);
            Assert.AreEqual(r5, false);
            Assert.AreEqual(s1, 1);
            Assert.AreEqual(s2, 33);
            Assert.AreEqual(s3, 5);
            Assert.AreEqual(s4, 983);
            Assert.AreEqual(s5, -1);
        }
    }
}
