using System;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Validates that each jump instruction to a label has that label in the instruction list
/// </summary>
public class ValidateJumpDestinationLabelsPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        foreach (var b in f.BlockList)
        {
            foreach (var t in b.Instructions)
            {
                if (t is Jump jmp)
                {
                    if (b.Instructions.IndexOf(jmp.Dest) == -1)
                    {
                        throw new Exception("Control flow is corrupted");
                    }
                }
            }
        }
    }
}