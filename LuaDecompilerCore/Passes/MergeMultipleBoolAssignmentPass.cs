using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// HKS as an optimization seems to optimize the following:
///     local a, b, c, d = false
/// as two instructions: LOADBOOL followed by LOADNIL. It seems that
/// LOADNIL can recognize when it's preceded by LOADBOOL and load a bool
/// instead of a nil into the register. This pass recognizes this idiom and
/// merges them back together. This only works when the bool is false as it's unknown
/// if it does this for true as well.
/// </summary>
public class MergeMultipleBoolAssignmentPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        foreach (var b in f.BlockList)
        {
            for (int i = 0; i < b.Instructions.Count - 2; i++)
            {
                if (b.Instructions[i] is Assignment a1 && a1.Left.Count > 0 && !a1.Left[0].HasIndex &&
                    a1.Right is Constant c && c.ConstType == Constant.ConstantType.ConstBool && !c.Boolean &&
                    b.Instructions[i + 1] is Assignment a2 && a2.Left.Count > 0 && !a2.Left[0].HasIndex &&
                    a2.Right is Constant c2 && c2.ConstType == Constant.ConstantType.ConstNil)
                {
                    a1.Left.AddRange(a2.Left);
                    if (a1.LocalAssignments == null)
                    {
                        a1.LocalAssignments = a2.LocalAssignments;
                    }
                    else
                    {
                        a1.LocalAssignments.AddRange(a2.LocalAssignments);
                    }
                    b.Instructions.RemoveAt(i + 1);
                }
            }
        }
    }
}