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
                // Match conditional jump to label
                if (b.Instructions[i] is Jump
                    {
                        Conditional: true
                    } jmp &&
                    // Register set to false
                    b.Instructions[i + 1] is Assignment
                    {
                        IsSingleAssignment: true, 
                        Left: { } assignee, 
                        Right: Constant { ConstType: Constant.ConstantType.ConstBool, Boolean: false }
                    } &&
                    // Unconditional jump
                    b.Instructions[i + 2] is Jump
                    {
                        Conditional: false
                    } jmp2 &&
                    // Label that is destination of original jump
                    b.Instructions[i + 3] is Label label1 && label1 == jmp.Dest &&
                    // Set same register to true
                    b.Instructions[i + 4] is Assignment
                    {
                        IsSingleAssignment: true,
                        Left: { } assignee2, 
                        Right: Constant { ConstType: Constant.ConstantType.ConstBool, Boolean: true }
                    } && assignee.Identifier == assignee2.Identifier &&
                    // Label that is destination of the second jump
                    b.Instructions[i + 5] is Label label2 && label2 == jmp2.Dest)
                {
                    if (jmp.Condition is BinOp bop)
                    {
                        bop.NegateCondition();
                    }
                    var newAssignment = new Assignment(assignee, jmp.Condition);
                    b.Instructions[i] = newAssignment;
                    
                    // Don't remove the final label as it can be a jump destination sometimes
                    b.Instructions.RemoveRange(i + 1, 4);
                }
            }
        }
    }
}