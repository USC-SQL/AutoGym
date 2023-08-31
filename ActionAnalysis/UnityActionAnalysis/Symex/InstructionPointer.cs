﻿using System;
using System.Diagnostics;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.TypeSystem;

namespace UnityActionAnalysis
{
    public class InstructionPointer
    {
        public readonly Block block;
        public readonly int index;

        public InstructionPointer(Block block, int index)
        {
            this.block = block;
            this.index = index;
        }

        public InstructionPointer(InstructionPointer o)
        {
            block = o.block;
            index = o.index;
        }

        public ILInstruction GetInstruction()
        {
            return block.Instructions[index];
        }

        public InstructionPointer NextInstruction()
        {
            return new InstructionPointer(block, index + 1);
        }

        public IMethod GetCurrentMethod()
        {
            return FindInstructionFunction(block).Method;
        }

        public static ILFunction FindInstructionFunction(ILInstruction inst)
        {
            while (!(inst is ILFunction)) {
                inst = inst.Parent;
            }
            return (ILFunction)inst;
        }
    }
}
