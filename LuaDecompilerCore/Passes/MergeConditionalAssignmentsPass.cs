﻿using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Sometimes a conditional assignment is generated in lua to implement something like:
/// local var = someFunction() == true and someOtherFunction == false
/// This will generate IR with a CFG equivalent to
/// local var
/// if someFunction() == true and someOtherFunction == false
///     var = true
/// else
///     var = false
///
/// This pass recognizes that pattern rewrites it into the inlined form
/// </summary>
public class MergeConditionalAssignmentsPass : IPass
{
    public void RunOnFunction(DecompilationContext decompilationContext, FunctionContext functionContext, Function f)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var b in f.BlockList)
            {
                if (b is
                    {
                        HasInstructions: true,
                        Last: ConditionalJump jump,
                        EdgeTrue:
                        {
                            Predecessors.Count: 1,
                            Successors: [{ Predecessors.Count: 2 } follow],
                            Instructions.Count: 2,
                            First: Assignment
                            {
                                IsSingleAssignment: true,
                                Left: { Identifier.IsRegister: true } falseAssignee,
                                Right: Constant { ConstType: Constant.ConstantType.ConstBool, Boolean: false }
                            } falseAssignment
                        } edgeTrue,
                        EdgeFalse:
                        {
                            Predecessors.Count: 1,
                            Successors.Count: 1,
                            Instructions.Count: 1,
                            First: Assignment
                            {
                                IsSingleAssignment: true,
                                Left: { Identifier.IsRegister: true } trueAssignee,
                                Right: Constant { ConstType: Constant.ConstantType.ConstBool, Boolean: true }
                            }
                        } edgeFalse
                    } && follow == edgeFalse.Successors[0] &&
                    falseAssignee.Identifier.RegNum == trueAssignee.Identifier.RegNum
                   )
                {
                    var destReg = falseAssignee.Identifier;
                    if (follow.PhiFunctions.TryGetValue(destReg.RegNum, out var phi))
                    {
                        if ((phi.Right.Count == 2 && phi.Right[0] == falseAssignee.Identifier &&
                            phi.Right[1] == trueAssignee.Identifier) || 
                            (phi.Right.Count == 2 && phi.Right[1] == falseAssignee.Identifier &&
                             phi.Right[0] == trueAssignee.Identifier))
                        {
                            follow.PhiFunctions.Remove(destReg.RegNum);
                            destReg = phi.Left;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    b.Last = falseAssignment;
                    falseAssignment.Block = b;
                    falseAssignment.Left.Identifier = destReg;
                    falseAssignment.Right = jump.Condition;
                    b.Successors = follow.Successors;
                    foreach (var instruction in follow.Instructions)
                    {
                        instruction.Block = b;
                    }

                    b.Instructions.AddRange(follow.Instructions);
                    foreach (var successor in b.Successors)
                    {
                        var idx = successor.Predecessors.FindIndex(predecessor => predecessor == follow);
                        successor.Predecessors[idx] = b;
                    }

                    f.BlockList.RemoveAll(block => block == edgeFalse || block == edgeTrue || block == follow);

                    changed = true;
                    break;
                }
            }
        }
    }
}