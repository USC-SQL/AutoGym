using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Z3;
using UnityActionAnalysis.Operations;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.TypeSystem;

namespace UnityActionAnalysis.Tests
{
    public class ConfigL : TestConfig
    {
        public override bool ShouldAbortBranchCase(BranchCase branchCase, ILInstruction branchInst, SymexState state)
        {
            IMethod method = branchCase.IP.GetCurrentMethod(); // test ability to find the method
            Assert.IsTrue(method.Name == "Main");
            return false;
        }
    }

    [TestClass()]
    public class TestL
    {
        [TestMethod()]
        public void TestPathConditions()
        {
            using (SymexMachine machine = SymexTestHelpers.CreateMachine("UnityActionAnalysisTestCases.Symex.TestL.ProgramL", "Main", new ConfigL()))
            {
                machine.Run();

                using (var z3 = new Context(new Dictionary<string, string>() { { "model", "true" } }))
                {
                    SymexTestHelpers.SymexMachineHelper helper = new SymexTestHelpers.SymexMachineHelper(machine, z3);
                    var arg0 = z3.MkConst("frame:0:arg:0", z3.MkBitVecSort(32));
                    var arg1 = z3.MkConst("frame:0:arg:1", z3.MkBitVecSort(32));

                    Assert.IsTrue(helper.ExistsState((s, m) => 
                    {
                        if (SymexTestHelpers.ModelContainsVariables(m, arg0)) {
                            int x = (int)uint.Parse(m.Evaluate(arg0).ToString());
                            return x == 2;
                        } else
                        {
                            return false;
                        }
                    }));

                    Assert.IsTrue(helper.ExistsState((s, m) => 
                    {
                        if (SymexTestHelpers.ModelContainsVariables(m, arg0)) {
                            int x = (int)uint.Parse(m.Evaluate(arg0).ToString());
                            return x == 3;
                        } else
                        {
                            return false;
                        }
                    }));

                    Assert.IsFalse(helper.ExistsState((s, m) => 
                    {
                        if (SymexTestHelpers.ModelContainsVariables(m, arg0)) {
                            int x = (int)uint.Parse(m.Evaluate(arg0).ToString());
                            return x == 1;
                        } else
                        {
                            return false;
                        }
                    }));

                    Assert.IsTrue(helper.ExistsState((s, m) => 
                    {
                        if (SymexTestHelpers.ModelContainsVariables(m, arg0, arg1)) {
                            int x = (int)uint.Parse(m.Evaluate(arg0).ToString());
                            int y = (int)uint.Parse(m.Evaluate(arg1).ToString());
                            return y == 44 && x + y == 111;
                        } else
                        {
                            return false;
                        }
                    }));

                    Assert.IsTrue(helper.ExistsState((s, m) => 
                    {
                        if (SymexTestHelpers.ModelContainsVariables(m, arg0, arg1)) {
                            int x = (int)uint.Parse(m.Evaluate(arg0).ToString());
                            int y = (int)uint.Parse(m.Evaluate(arg1).ToString());
                            return y == 77 && x + y == 111;
                        } else
                        {
                            return false;
                        }
                    }));
                } 
            }
        }
    }
}
