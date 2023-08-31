using System;
using System.Collections.Generic;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;
using Microsoft.Z3;
using UnityActionAnalysis.Operations;

namespace UnityActionAnalysis
{
    public abstract class Configuration
    {
        public virtual void InitializeStates()
        {
        }

        public abstract bool IsMethodSummarized(IMethod method);

        public virtual void ApplyMethodSummary(IMethod method, List<Expr> arguments, Variable resultVar, SymexState state)
        {
            int symId = state.symcallCounter++;
            Expr value = MakeSymcall(method, arguments, symId, state);
            state.MemoryWrite(resultVar.address, value);
        }

        protected Expr MakeSymcall(IMethod method, List<Expr> arguments, int symId, SymexState state, bool firstCall = true)
        {
            string name = "symcall:" + symId;
            Expr value = state.MakeSymbolicValue(method.IsConstructor ? method.DeclaringType : method.ReturnType, name);
            if (firstCall)
            {
                state.symbolicMethodCalls[symId] = new SymbolicMethodCall(method, arguments);
            } else
            {
                if (!state.symbolicMethodCalls.ContainsKey(symId))
                {
                    throw new Exception("Cannot find existing symcall " + symId);
                }
            }
            return value;
        }

        public virtual bool ShouldAbortBranchCase(BranchCase branchCase, ILInstruction branchInst, SymexState state)
        {
            return false;
        }

        public virtual object NewStateCustomData()
        {
            return null;
        }

        public virtual object CloneStateCustomData(object data)
        {
            return null;
        }
    }
}