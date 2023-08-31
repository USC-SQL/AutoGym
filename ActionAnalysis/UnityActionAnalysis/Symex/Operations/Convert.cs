using System;
using System.Collections.Generic;
using Microsoft.Z3;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;

namespace UnityActionAnalysis.Operations
{
    public class Convert : Operation
    {
        private Variable valueVar;
        private IType typeTo;
        private Variable resultVar;

        public Convert(Variable valueVar, IType typeTo, Variable resultVar, ILInstruction inst) : base(inst)
        {
            this.valueVar = valueVar;
            this.typeTo = typeTo;
            this.resultVar = resultVar;
        }

        public override void Perform(SymexState state)
        {
            Context z3 = SymexMachine.Instance.Z3;
            IType typeFrom = valueVar.type;
            Sort sortFrom = SymexMachine.Instance.SortPool.TypeToSort(typeFrom);
            Sort sortTo = SymexMachine.Instance.SortPool.TypeToSort(typeTo);
            Expr valueFrom = state.MemoryRead(valueVar.address, valueVar.type);
            Expr result;

            if (sortFrom is IntSort || sortTo is IntSort)
            {
                if (!(sortFrom is IntSort) || !(sortTo is IntSort))
                {
                    throw new Exception("unexpected conversion between reference type and value type");
                }
                result = valueFrom;
            }
            else if (sortFrom is BitVecSort && sortTo is BitVecSort)
            {
                uint bitsFrom = ((BitVecSort)sortFrom).Size;
                uint bitsTo = ((BitVecSort)sortTo).Size;
                if (bitsFrom < bitsTo)
                {
                    // widen
                    uint delta = bitsTo - bitsFrom;
                    result = z3.MkConcat(z3.MkBV(0, delta), (BitVecExpr)valueFrom);
                } else
                {
                    // narrow
                    result = z3.MkExtract(bitsTo - 1, 0, (BitVecExpr)valueFrom);
                }
            }
            else if (sortFrom is BitVecSort && sortTo is RealSort)
            {
                bool fromSigned = SymexMachine.Instance.SortPool.IsSigned(typeFrom);
                result = z3.MkInt2Real(z3.MkBV2Int((BitVecExpr)valueFrom, fromSigned));
            }
            else if (sortFrom is RealSort && sortTo is BitVecSort)
            {
                result = z3.MkInt2BV(((BitVecSort)sortTo).Size, z3.MkReal2Int((RealExpr)valueFrom));
            }
            else if (sortFrom is RealSort && sortTo is RealSort)
            {
                result = (RealExpr)valueFrom;
            }
            else
            {
                throw new Exception("unexpected conversion from " + typeFrom.FullName + " to " + typeTo.FullName + " (sortFrom " + sortFrom.GetType() + ", sortTo " + sortTo.GetType() + ")");
            }

            state.MemoryWrite(resultVar.address, result);
        }
    }
}
