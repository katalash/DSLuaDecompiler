using System;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

public class MergeConditionalJumpsPass : IPass
{
    /// <summary>
    /// Simple analysis pass to recognize lua conditional jumping patterns and merge them into a single instruction
    /// </summary>
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        // Lua conditional jumps often follow this pattern when naively translated into the IR:
        //   if REGA == b then goto Label_1:
        //   goto Label_2:
        //   Label_1:
        //   ...
        //
        // This pass recognizes and simplifies this into:
        //   if REGA ~= b then goto Label_2:
        //   ...
        //
        // This will greatly simplify the generated control flow graph, so this is done first
        // This algorithm is run until convergence
        foreach (var b in f.BlockList)
        {
            for (int i = 0; i < b.Instructions.Count - 2; i++)
            {
                // Pattern match the prerequisites
                if (b.Instructions[i] is Jump jmp1 && jmp1.Conditional &&
                    b.Instructions[i + 1] is Jump jmp2 && !jmp2.Conditional &&
                    b.Instructions[i + 2] is Label shortLabel && jmp1.Dest == shortLabel)
                {
                    // flip the condition and change the destination to the far jump. Then remove the following goto and label
                    if (jmp1.Condition is BinOp op)
                    {
                        op.NegateCondition();
                        jmp1.Dest.UsageCount--;
                        b.Instructions.RemoveRange(i + 1, jmp1.Dest.UsageCount <= 0 ? 2 : 1);
                        jmp1.Dest = jmp2.Dest;
                    }
                    else if ((jmp1.Condition is UnaryOp op2 && op2.Operation == UnaryOp.OperationType.OpNot) || jmp1.Condition is IdentifierReference)
                    {
                        jmp1.Dest.UsageCount--;
                        b.Instructions.RemoveRange(i + 1, jmp1.Dest.UsageCount <= 0 ? 2 : 1);
                        jmp1.Dest = jmp2.Dest;
                    }
                    else
                    {
                        throw new Exception("Recognized jump pattern does not use a binary op conditional");
                    }
                }
            }
        }
    }
}