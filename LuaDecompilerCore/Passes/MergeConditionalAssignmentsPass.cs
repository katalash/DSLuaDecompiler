using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Sometimes a conditional assignment is generated in lua to implement something like:
/// local var = somefunction() == true
///
/// This would generate the following IR:
/// if REGA == 1234 else goto label_1
/// REGB = false
/// goto label_2
/// label_1:
/// REGB = true
/// label_2:
/// ...
/// This pattern matches such a case and replaces it with just:
/// REGB = REGA ~= 1234
/// </summary>
public class MergeConditionalAssignmentsPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        foreach (var b in f.BlockList)
        {
            for (int i = 0; i < b.Instructions.Count - 6; i++)
            {
                // Big pattern match
                if (b.Instructions[i] is Jump { Conditional: true } jmp &&
                    b.Instructions[i + 1] is Assignment asscond1 && asscond1.Left.Count == 1 && asscond1.Left[0] is { } assignee && 
                    asscond1.Right is Constant { ConstType: Constant.ConstantType.ConstBool, Boolean: false } &&
                    b.Instructions[i + 2] is Jump jmp2 && !jmp2.Conditional &&
                    b.Instructions[i + 3] is Label label1 && label1 == jmp.Dest &&
                    b.Instructions[i + 4] is Assignment asscond2 && asscond2.Left.Count == 1 && asscond2.Left[0] is { } assignee2 && 
                    assignee.Identifier == assignee2.Identifier && asscond2.Right is Constant { ConstType: Constant.ConstantType.ConstBool, Boolean: true } &&
                    b.Instructions[i + 5] is Label label2 && label2 == jmp2.Dest)
                {
                    if (jmp.Condition is BinOp bop)
                    {
                        bop.NegateCondition();
                    }
                    var newassn = new Assignment(assignee, jmp.Condition);
                    b.Instructions[i] = newassn;
                    
                    // Don't remove the final label as it can be a jump destination sometimes
                    b.Instructions.RemoveRange(i + 1, 4);
                }
            }
        }
    }
}