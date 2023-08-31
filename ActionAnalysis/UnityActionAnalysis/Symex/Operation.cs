using System;
using System.Collections.Generic;
using System.Text;
using ICSharpCode.Decompiler.IL;

namespace UnityActionAnalysis
{
    public abstract class Operation
    {
        // Instruction associated with the operation (may be null if this is a custom operation)
        public ILInstruction Instruction { get; private set; }

        public Operation(ILInstruction instruction)
        {
            Instruction = instruction;
        }

        public abstract void Perform(SymexState state);
    }


}
