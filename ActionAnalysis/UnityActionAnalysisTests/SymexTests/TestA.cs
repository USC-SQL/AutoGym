using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Z3;

namespace UnityActionAnalysis.Tests
{
    [TestClass()]
    public class TestA
    {
        [TestMethod()]
        public void TestPathConditions()
        {
            using (SymexMachine machine = SymexTestHelpers.CreateMachine("UnityActionAnalysisTestCases.Symex.TestA.ProgramA", "Main", new TestConfig()))
            {
                machine.Run();

                SymexTestHelpers.CommonAssertionsAfterRun(machine);

                Assert.AreEqual(3, machine.States.Count);
                Assert.AreEqual(3, machine.States.Where(s => s.execStatus == ExecutionStatus.HALTED).Count());

                using (var z3 = new Context(new Dictionary<string, string>() { { "model", "true" } }))
                {
                    SymexTestHelpers.SymexMachineHelper helper = new SymexTestHelpers.SymexMachineHelper(machine, z3);

                    var arg0 = z3.MkConst("frame:0:arg:0", z3.MkBitVecSort(32));
                    var arg1 = z3.MkConst("frame:0:arg:1", z3.MkBitVecSort(32));
                    Assert.IsTrue(helper.ExistsState((s, m) =>
                    {
                        if (SymexTestHelpers.ModelContainsVariables(m, arg0, arg1))
                        {
                            int x = int.Parse(m.Evaluate(arg0).ToString());
                            int y = int.Parse(m.Evaluate(arg1).ToString());
                            return x > 0 && y > 0;
                        }
                        else
                        {
                            return false;
                        }
                    }));
                    Assert.IsTrue(helper.ExistsState((s, m) =>
                    {
                        if (SymexTestHelpers.ModelContainsVariables(m, arg0, arg1))
                        {
                            int x = int.Parse(m.Evaluate(arg0).ToString());
                            int y = int.Parse(m.Evaluate(arg1).ToString());
                            return x > 0 && y <= 0;
                        }
                        else
                        {
                            return false;
                        }
                    }));
                    Assert.IsTrue(helper.ExistsState((s, m) =>
                    {
                        if (SymexTestHelpers.ModelContainsVariables(m, arg0))
                        {
                            int x = int.Parse(m.Evaluate(arg0).ToString());
                            return x <= 0;
                        }
                        else
                        {
                            return false;
                        }
                    }));
                }
            }
        }
    }
}