using Microsoft.Z3;
using System;
using ICSharpCode.Decompiler.IL;

namespace UnityActionAnalysis.Operations
{
    public class Custom : Operation
    {
        private Action<SymexState> handler;

        public Custom(Action<SymexState> handler, ILInstruction inst = null) : base(inst)
        {
            this.handler = handler;
        }

        public override void Perform(SymexState state)
        {    
            handler(state);
        }
    }
}
