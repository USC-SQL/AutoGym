using System;
using System.Collections.Generic;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;

namespace UnityActionAnalysis
{
    public class ReachableMethods
    {
        private IMethod entryPoint;
        private MethodPool pool;

        public ReachableMethods(IMethod entryPoint, MethodPool pool)
        {
            this.entryPoint = entryPoint;
            this.pool = pool;
        }

        private IEnumerable<IMethod> CheckCallInstruction(ILInstruction inst, ISet<IMethod> visited)
        {
            if (AnalysisHelpers.FindCallInstruction(inst, out CallInstruction callInst))
            {
                IMethod target = callInst.Method;
                if (!visited.Contains(target))
                {
                    foreach (IMethod m in DoFindReachable(target, visited))
                    {
                        yield return m;
                    }
                }
            }
        }

        private IEnumerable<IMethod> DoFindReachable(IMethod m, ISet<IMethod> visited)
        {
            yield return m;
            visited.Add(m);
            if (AnalysisHelpers.ShouldProcessBody(m, entryPoint))
            {
                ILFunction func = InstructionPointer.FindInstructionFunction(pool.MethodEntryPoint(m).block);
                foreach (ILInstruction inst in AnalysisHelpers.Instructions(func))
                {
                    foreach (IMethod method in CheckCallInstruction(inst, visited))
                    {
                        yield return method;
                    }
                }
            }
        }

        public IEnumerable<IMethod> FindReachableMethods()
        {
            HashSet<IMethod> visited = new HashSet<IMethod>();
            foreach (IMethod m in DoFindReachable(entryPoint, visited))
            {
                yield return m;
            }
        }

    }

}
