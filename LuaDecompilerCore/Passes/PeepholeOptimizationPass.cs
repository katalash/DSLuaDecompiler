using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Optimizes out jumps to jumps, and deletes labels too if they become unused as a result
/// </summary>
public class PeepholeOptimizationPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        foreach (var b in f.BlockList)
        {
            for (int i = 0; i < b.Instructions.Count; i++)
            {
                if (b.Instructions[i] is Jump jmp1)
                {
                    Instruction dest = b.Instructions[b.Instructions.IndexOf(jmp1.Dest) + 1];
                    while (dest is Jump { Conditional: false } jmp2)
                    {
                        jmp1.Dest.UsageCount--;
                        if (jmp1.Dest.UsageCount <= 0)
                        {
                            b.Instructions.Remove(jmp1.Dest);
                        }
                        jmp1.Dest = jmp2.Dest;
                        dest = b.Instructions[b.Instructions.IndexOf(jmp1.Dest) + 1];
                    }
                }
            }
        }
    }
}