using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.ControlFlow;
using ICSharpCode.Decompiler.FlowAnalysis;

namespace UnityActionAnalysis
{
    public static class AnalysisHelpers
    {
        public static bool ShouldProcessBody(IMethod m, IMethod entryPoint)
        {
            if (!m.HasBody || UnityConfiguration.IsInputAPI(m))
            {
                return false;
            }
            return m.ParentModule == entryPoint.ParentModule;
        }

        private static IEnumerable<ILInstruction> DoEnumerateInstructions(BlockContainer bc)
        {
            foreach (Block b in bc.Blocks)
            {
                var blockInsts = b.Instructions;
                for (int i = 0, n = blockInsts.Count; i < n; ++i)
                {
                    ILInstruction inst = blockInsts[i];
                    if (i == n-1 && inst is Branch && inst.ILRangeIsEmpty)
                    {
                        // ignore br instruction generated at the end of conditional blocks
                        continue;
                    }
                    yield return inst;
                }
            }
        }

        public static IEnumerable<ILInstruction> Instructions(ILFunction func)
        {
            foreach (ILInstruction inst in DoEnumerateInstructions((BlockContainer)func.Body))
            {
                yield return inst;
            }
        }

        public static bool FindCallInstruction(ILInstruction inst, out CallInstruction result)
        {
            if (inst is CallInstruction callinst)
            {
                result = callinst;
                return true;
            } else
            {
                foreach (ILInstruction child in inst.Children)
                {
                    if (FindCallInstruction(child, out result))
                    {
                        return true;
                    }
                }
                result = null;
                return false;
            }
        }

        public static IEnumerable<ILInstruction> Predecessors(ILInstruction inst, ControlFlowGraph cfg)
        {
            Block b = (Block)inst.Parent;
            if (inst.ChildIndex > 0)
            {
                yield return b.Instructions[inst.ChildIndex - 1];
            } else
            {
                ControlFlowNode cfgNode = cfg.GetNode(b);
                foreach (ControlFlowNode p in cfgNode.Predecessors)
                {
                    Block predBlock = (Block)p.UserData;
                    ILInstruction predInst = predBlock.Instructions.Last();
                    if (predInst is Branch && predInst.ILRangeIsEmpty) 
                    {
                        // ignore br instruction generated at the end of conditional blocks
                        Debug.Assert(Predecessors(predInst, cfg).Count() == 1);
                        predInst = Predecessors(predInst, cfg).First();
                    }
                    yield return predInst;
                }
            }
        }

        public static IEnumerable<ILInstruction> Successors(ILInstruction inst, ControlFlowGraph cfg)
        {
            Block b = (Block)inst.Parent;
            if (inst.ChildIndex < b.Instructions.Count - 1)
            {
                int succIndex = inst.ChildIndex + 1;
                ILInstruction succInst = b.Instructions[inst.ChildIndex + 1];
                if (succIndex == b.Instructions.Count - 1 && succInst is Branch && succInst.ILRangeIsEmpty)
                {
                    // ignore br instruction generated at the end of conditional blocks
                    foreach (ILInstruction realSuccInst in Successors(succInst, cfg))
                    {
                        yield return realSuccInst;
                    }
                } else
                {
                    yield return succInst;
                }
            }
            else
            {
                ControlFlowNode cfgNode = cfg.GetNode(b);
                foreach (ControlFlowNode succ in cfgNode.Successors)
                {
                    Block succBlock = (Block)succ.UserData;
                    yield return succBlock.Instructions.First();
                }
            }
        }

        public static string MethodSignature(IMethod method)
        {
            return Helpers.GetMethodSignature(method);
        }

        public static string FieldSignature(IField field)
        {
            return field.DeclaringType.FullName + "." + field.Name;
        }

        public static bool ExpressionContainsAnyVariable(ILInstruction val, ISet<string> variableNames)
        {
            if (val is LdLoc ldloc)
            {
                return variableNames.Contains(ldloc.Variable.Name);
            } else if (val is LdLoca ldloca)
            {
                return variableNames.Contains(ldloca.Variable.Name);
            } else
            {
                foreach (ILInstruction child in val.Children)
                {
                    if (ExpressionContainsAnyVariable(child, variableNames))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public static bool ExpressionUsesAnyVariable(ILInstruction inst, ISet<string> variableNames)
        {
            if (inst is StLoc stloc)
            {
                return ExpressionContainsAnyVariable(stloc.Value, variableNames);
            } else
            {
                return ExpressionContainsAnyVariable(inst, variableNames);
            }
        }

        public static bool ExpressionContainsAnyField(ILInstruction val, ISet<string> fieldSignatures)
        {
            if (val is LdFlda ldflda)
            {
                return fieldSignatures.Contains(FieldSignature(ldflda.Field));
            } else if (val is LdsFlda ldsflda)
            {
                return fieldSignatures.Contains(FieldSignature(ldsflda.Field));
            } else
            {
                foreach (ILInstruction child in val.Children)
                {
                    if (ExpressionContainsAnyField(child, fieldSignatures))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public static bool ExpressionContainsAnyMethod(ILInstruction val, ISet<string> methodSignatures)
        {
            if (val is CallInstruction callinst)
            {
                return methodSignatures.Contains(MethodSignature(callinst.Method));
            } else
            {
                foreach (ILInstruction child in val.Children)
                {
                    if (ExpressionContainsAnyMethod(child, methodSignatures))
                    {
                        return true;
                    }
                }
                return false;    
            }
        }

        public static bool ExpressionContainsAnyInputAPI(ILInstruction val)
        {
            if (val is CallInstruction callinst)
            {
                return UnityConfiguration.IsInputAPI(callinst.Method);
            } else
            {
                foreach (ILInstruction child in val.Children)
                {
                    if (ExpressionContainsAnyInputAPI(child))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public static bool FindInstruction<T>(ILInstruction inst, out T result) where T : ILInstruction
        {
            if (inst is T target)
            {
                result = target;
                return true;
            } else
            {
                foreach (ILInstruction child in inst.Children)
                {
                    if (FindInstruction(child, out result))
                    {
                        return true;
                    }
                }
                result = null;
                return false;
            }
        }
    }
}
