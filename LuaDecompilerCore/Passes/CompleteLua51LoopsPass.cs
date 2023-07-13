using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Finishes implementing the last part of the Lua 5.1 FORLOOP op.
/// I.e. in the block that follows the loop head that doesn't
/// break the loop insert the following IR: R(A+3) := R(A)
/// </summary>
public class CompleteLua51LoopsPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        foreach (var b in f.BlockList)
        {
            if (b.Instructions.Count > 0 && b.Instructions.Last() is Jump { PostTakenAssignment: not null } jmp)
            {
                b.Successors[1].Instructions.Insert(0, jmp.PostTakenAssignment);
                jmp.PostTakenAssignment.PropogateAlways = true;
                jmp.PostTakenAssignment.Block = b.Successors[1];
                jmp.PostTakenAssignment = null;
            }
        }
    }
}