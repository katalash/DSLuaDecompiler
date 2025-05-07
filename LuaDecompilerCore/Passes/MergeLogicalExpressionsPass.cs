using System;
using LuaDecompilerCore.Analyzers;
using LuaDecompilerCore.CFG;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// A logical expression is an expression not in an "if" statement that contains the logical operators "and" and "or".
/// For example, you may have an expression such as "local a = b and c or d" which is the equivalent to
/// "int a = b ? c : d" in C. 
/// </summary>
public class MergeLogicalExpressionsPass : IPass
{
    public bool MutatesCfg => true;

    private static Expression LogicalRight(Expression exp)
    {
        while (exp is BinOp { IsBooleanOp: true } b)
        {
            exp = b.Right;
        }

        return exp;
    }

    private static void NotLogicalRight(ConditionalJump a)
    {
        if (a.Condition is not BinOp { IsBooleanOp: true } right)
        {
            if (a.Condition is UnaryOp { Operation: UnaryOp.OperationType.OpNot, IsImplicit: true } op)
            {
                a.Condition = op.Expression;
            }
            return;
        }

        while (right.Right is BinOp { IsBooleanOp: true } r)
        {
            right = r;
        }

        if (right.Right is UnaryOp { Operation: UnaryOp.OperationType.OpNot, IsImplicit: true } op2)
        {
            right.Right = op2.Expression;
        }
    }
    
    private static void ReplaceLogicalRight(Assignment a, Expression replacement)
    {
        if (a.Right is not BinOp { IsBooleanOp: true } right)
        {
            a.Right = replacement;
            return;
        }

        while (right.Right is BinOp { IsBooleanOp: true } r)
        {
            right = r;
        }

        right.Right = replacement;
    }

    /// <summary>
    /// Attempts to detect the following pattern:
    ///   basicblock_1:
    ///     if REG0_0 else goto basicblock_3
    ///   basicblock_2:
    ///     REG2_0 = REG0_0
    ///     goto basicblock_4
    ///   basicblock_3:
    ///     REG2_1 = REG1_0
    ///   basicblock_4:
    ///     REG2_2 = phi(REG2_0, REG2_1)
    /// and transform it into:
    /// REG2_2 = REG0_0 and REG1_0
    ///
    /// This also handles the "or" variant, which has a not in the conditional
    /// </summary>
    private static bool MergeBasicPattern(Function f, BasicBlock b, IdentifierDefinitionUseAnalyzer useAnalysis)
    {
        if (b is
            {
                HasInstructions: true,
                Last: ConditionalJump jump,
                EdgeTrue:
                {
                    Predecessors.Count: 1,
                    Successors: [{ } follow],
                    Instructions.Count: 2,
                    First: Assignment
                    {
                        IsSingleAssignment: true,
                        Left: IdentifierReference { Identifier.IsRegister: true } trueAssignee,
                        Right: IdentifierReference { Identifier.IsRegister: true } conditionReg
                    } trueAssignment
                } edgeTrue,
                EdgeFalse:
                {
                    Predecessors.Count: 1,
                    Successors.Count: 1,
                    Instructions.Count: 1,
                    First: Assignment
                    {
                        IsSingleAssignment: true,
                        Left: IdentifierReference { Identifier.IsRegister: true } falseAssignee,
                        Right: not null
                    } falseAssignment
                } edgeFalse
            } && follow == edgeFalse.Successors[0] &&
            trueAssignee.Identifier.RegNum == falseAssignee.Identifier.RegNum
           )
        {
            bool isOr = false;
            var rightExp = LogicalRight(jump.Condition);
            if (rightExp is UnaryOp { Operation: UnaryOp.OperationType.OpNot, IsImplicit: true } op)
            {
                isOr = true;
                rightExp = op.Expression;
            }

            if (rightExp is not IdentifierReference)
            {
                return false;
            }

            var destReg = trueAssignee.Identifier;
                    
            if (follow.PhiFunctions.TryGetValue(destReg.RegNum, out var phi))
            {
                if (!phi.Right.Contains(trueAssignee.Identifier) || !phi.Right.Contains(falseAssignee.Identifier) ||
                    useAnalysis.UseCount(trueAssignee.Identifier) != 1 || 
                    useAnalysis.UseCount(falseAssignee.Identifier) != 1)
                    return false;
                
                if ((phi.Right.Count == 2 && phi.Right[0] == trueAssignee.Identifier &&
                     phi.Right[1] == falseAssignee.Identifier) || 
                    (phi.Right.Count == 2 && phi.Right[1] == trueAssignee.Identifier &&
                     phi.Right[0] == falseAssignee.Identifier))
                {
                    // If there's only two phi inputs we can remove them and the phi function
                    follow.PhiFunctions.Remove(destReg.RegNum);
                    destReg = phi.Left;
                }
                else
                {
                    // Otherwise just remove the phi for the false
                    phi.Right.Remove(falseAssignee.Identifier);
                }
            }
            else if (useAnalysis.UseCount(trueAssignee.Identifier) != 0 || 
                     useAnalysis.UseCount(falseAssignee.Identifier) != 0)
            {
                return false;
            }

            // Build compound condition
            NotLogicalRight(jump);
            var compound = new BinOp(jump.Condition, falseAssignment.Right,
                isOr ? BinOp.OperationType.OpOr : BinOp.OperationType.OpAnd)
            {
                MergedCompoundConditional = BinOp.MergedCompoundConditionalType.MergedLeft
            };
            compound.SolveConditionalExpression();
                    
            // Migrate Condition to instruction
            b.Last = trueAssignment;
            trueAssignment.OriginalBlock = b.BlockId;
            trueAssignee.Identifier = destReg;
            trueAssignment.Right = compound;
            //ReplaceLogicalRight(trueAssignment, compound);
            trueAssignment.Absorb(jump);
            trueAssignment.Absorb(falseAssignment);
                    
            // See if we can merge with the follow
            edgeTrue.ClearSuccessors();
            edgeFalse.ClearSuccessors();
            b.ClearSuccessors();

            if (follow.Predecessors.Count == 0)
            {
                if (follow.PhiFunctions.TryGetValue(destReg.RegNum, out var phi2))
                {
                    trueAssignee.Identifier = phi2.Left;
                    follow.PhiFunctions.Remove(destReg.RegNum);
                }
                
                b.StealSuccessors(follow);
                b.AbsorbInstructions(follow, true);
                f.RemoveBlockAt(follow.BlockIndex);
            }
            else
            {
                b.AddSuccessor(follow);
            }

            f.RemoveAllBlocks(block => block == edgeFalse || block == edgeTrue);

            return true;
        }

        return false;
    }
    
    /// <summary>
    /// detects and merges expression (and|or) expression (and|or) expression pattern where the middle part is a complex
    /// (i.e. not a simple register reference) expression
    /// </summary>
    private static bool MergeChainPattern(Function f, BasicBlock b, IdentifierDefinitionUseAnalyzer useAnalysis)
    {
        if (b is
            {
                HasInstructions: true,
                Last: ConditionalJump jump,
                EdgeTrue:
                {
                    Predecessors.Count: 1,
                    Successors: [
                        {
                            Predecessors.Count: <= 2,
                            Instructions: [
                                Assignment
                                {
                                    IsSingleAssignment: true,
                                    Left: IdentifierReference { Identifier.IsRegister: true } alternateAssignee,
                                    Right: { }
                                } alternateAssignment
                            ],
                            Successors: [{ } follow]
                        } trueTrue,
                        { } trueFollow],
                    Instructions: [
                        Assignment
                        {
                            IsSingleAssignment: true,
                            Left: IdentifierReference { Identifier.IsRegister: true } trueAssignee,
                            Right: { }
                        } trueAssignment,
                        ConditionalJump
                        {
                            Condition: IdentifierReference { IsRegister: true } or 
                                UnaryOp
                                {
                                    Operation: UnaryOp.OperationType.OpNot, 
                                    Expression: IdentifierReference { IsRegister: true }
                                }
                        } trueCondition
                    ]
                } edgeTrue,
                EdgeFalse: { } edgeFalse
            } && follow == trueFollow && (edgeFalse == trueTrue || edgeFalse == follow) &&
            trueAssignee.Identifier.RegNum == alternateAssignee.Identifier.RegNum
           )
        {
            bool isFirstOr = false;
            var rightExp = LogicalRight(jump.Condition);
            if (rightExp is UnaryOp { Operation: UnaryOp.OperationType.OpNot, IsImplicit: true })
            {
                isFirstOr = true;
            }
            
            bool isTrueOr = false;
            var rightTrueExp = trueCondition.Condition;
            if (rightTrueExp is UnaryOp { Operation: UnaryOp.OperationType.OpNot, IsImplicit: true } op2)
            {
                isTrueOr = true;
                rightTrueExp = op2.Expression;
            }

            if (rightTrueExp is not IdentifierReference ir || ir.Identifier.RegNum != trueAssignee.Identifier.RegNum)
            {
                return false;
            }

            var destReg = trueAssignee.Identifier;
                    
            if (follow.PhiFunctions.TryGetValue(destReg.RegNum, out var phi))
            {
                if (!phi.Right.Contains(trueAssignee.Identifier) || !phi.Right.Contains(alternateAssignee.Identifier))
                    return false;
                
                if ((phi.Right.Count == 2 && phi.Right[0] == trueAssignee.Identifier &&
                     phi.Right[1] == alternateAssignee.Identifier) || 
                    (phi.Right.Count == 2 && phi.Right[1] == trueAssignee.Identifier &&
                     phi.Right[0] == alternateAssignee.Identifier))
                {
                    // If there's only two phi inputs we can remove them and the phi function
                    follow.PhiFunctions.Remove(destReg.RegNum);
                    destReg = phi.Left;
                }
                else
                {
                    // Otherwise just remove the phi for the alternate
                    phi.Right.Remove(alternateAssignee.Identifier);
                }
            }
            else if (useAnalysis.UseCount(trueAssignee.Identifier) != 0 || 
                     useAnalysis.UseCount(alternateAssignee.Identifier) != 0)
            {
                return false;
            }

            // Build compound condition
            NotLogicalRight(trueCondition);
            NotLogicalRight(jump);
            var compound = new BinOp(new BinOp(jump.Condition, trueAssignment.Right,
                isFirstOr ? BinOp.OperationType.OpOr : BinOp.OperationType.OpAnd)
            {
                MergedCompoundConditional = BinOp.MergedCompoundConditionalType.MergedLeft
            }, alternateAssignment.Right, isTrueOr ? BinOp.OperationType.OpOr : BinOp.OperationType.OpAnd)
            {
                MergedCompoundConditional = BinOp.MergedCompoundConditionalType.MergedLeft
            };
            compound.SolveConditionalExpression();
                    
            // Migrate Condition to instruction
            b.Last = trueAssignment;
            trueAssignment.OriginalBlock = b.BlockId;
            trueAssignee.Identifier = destReg;
            trueAssignment.Right = compound;
            //ReplaceLogicalRight(trueAssignment, compound);
            trueAssignment.Absorb(jump);
            trueAssignment.Absorb(trueAssignment);
            trueAssignment.Absorb(trueCondition);
            trueAssignment.Absorb(alternateAssignment);
                    
            // See if we can merge with the follow
            edgeTrue.ClearSuccessors();
            trueTrue.ClearSuccessors();
            b.ClearSuccessors();

            if (follow.Predecessors.Count == 0)
            {
                if (follow.PhiFunctions.TryGetValue(destReg.RegNum, out var phi2))
                {
                    trueAssignee.Identifier = phi2.Left;
                    follow.PhiFunctions.Remove(destReg.RegNum);
                }
                
                b.StealSuccessors(follow);
                b.AbsorbInstructions(follow, true);
                f.RemoveBlockAt(follow.BlockIndex);
            }
            else
            {
                b.AddSuccessor(follow);
            }

            f.RemoveAllBlocks(block => block == trueTrue || block == edgeTrue);

            return true;
        }

        return false;
    }

    private static bool MergeChainPattern2(Function f, BasicBlock b, IdentifierDefinitionUseAnalyzer useAnalysis)
    {
        if (b is
            {
                HasInstructions: true,
                Last: ConditionalJump jump,
                EdgeTrue:
                {
                    Predecessors.Count: 1,
                    Successors: [
                        {
                            Predecessors.Count: 1,
                            Instructions: [
                                Assignment
                                {
                                    IsSingleAssignment: true,
                                    Left: IdentifierReference { Identifier.IsRegister: true } alternateAssignee,
                                    Right: IdentifierReference { Identifier.IsRegister: true } alternateAssigned,
                                } alternateAssignment,
                                Jump
                            ],
                            Successors: [{ } follow]
                        } trueTrue,
                        {
                            Predecessors.Count: >= 2,
                            Instructions: [
                                Assignment
                                {
                                    IsSingleAssignment: true,
                                    Left: IdentifierReference { Identifier.IsRegister: true } falseAssignee,
                                    Right: not null
                                } falseAssignment
                            ],
                            Successors: [{ } falseFollow]
                        } trueFalse],
                    Instructions: [
                        Assignment
                        {
                            IsSingleAssignment: true,
                            Left: IdentifierReference { Identifier.IsRegister: true } trueAssignee,
                            Right: not null
                        } trueAssignment,
                        ConditionalJump
                        {
                            Condition: IdentifierReference { IsRegister: true } or 
                                UnaryOp
                                {
                                    Operation: UnaryOp.OperationType.OpNot, 
                                    Expression: IdentifierReference { IsRegister: true }
                                }
                        } trueCondition
                    ]
                } edgeTrue,
                EdgeFalse: { } edgeFalse
            } && edgeFalse == trueFalse && follow == falseFollow &&
            alternateAssignee.Identifier.RegNum == falseAssignee.Identifier.RegNum
           )
        {
            bool isFirstOr = false;
            var rightExp = LogicalRight(jump.Condition);
            if (rightExp is UnaryOp { Operation: UnaryOp.OperationType.OpNot, IsImplicit: true })
            {
                isFirstOr = true;
            }
            
            bool isTrueOr = false;
            var rightTrueExp = trueCondition.Condition;
            if (rightTrueExp is UnaryOp { Operation: UnaryOp.OperationType.OpNot, IsImplicit: true } op2)
            {
                isTrueOr = true;
                rightTrueExp = op2.Expression;
            }

            if (rightTrueExp is not IdentifierReference ir || ir.Identifier.RegNum != trueAssignee.Identifier.RegNum ||
                ir.Identifier != alternateAssigned.Identifier)
            {
                return false;
            }

            var destReg = alternateAssignee.Identifier;
                    
            if (follow.PhiFunctions.TryGetValue(destReg.RegNum, out var phi))
            {
                if (!phi.Right.Contains(alternateAssignee.Identifier) || !phi.Right.Contains(falseAssignee.Identifier))
                    return false;
                
                if ((phi.Right.Count == 2 && phi.Right[0] == alternateAssignee.Identifier &&
                     phi.Right[1] == falseAssignee.Identifier) || 
                    (phi.Right.Count == 2 && phi.Right[1] == alternateAssignee.Identifier &&
                     phi.Right[0] == falseAssignee.Identifier))
                {
                    // If there's only two phi inputs we can remove them and the phi function
                    follow.PhiFunctions.Remove(destReg.RegNum);
                    destReg = phi.Left;
                }
                else
                {
                    // Otherwise just remove the phi for the false
                    phi.Right.Remove(falseAssignee.Identifier);
                }
            }
            else if (useAnalysis.UseCount(alternateAssignee.Identifier) != 0 || 
                     useAnalysis.UseCount(falseAssignee.Identifier) != 0 ||
                     useAnalysis.UseCount(trueAssignee.Identifier) != 2)
            {
                return false;
            }

            // Build compound condition
            NotLogicalRight(trueCondition);
            NotLogicalRight(jump);
            var compound = new BinOp(new BinOp(jump.Condition, trueAssignment.Right,
                isFirstOr ? BinOp.OperationType.OpOr : BinOp.OperationType.OpAnd)
            {
                MergedCompoundConditional = BinOp.MergedCompoundConditionalType.MergedLeft
            }, falseAssignment.Right, isTrueOr ? BinOp.OperationType.OpOr : BinOp.OperationType.OpAnd)
            {
                MergedCompoundConditional = BinOp.MergedCompoundConditionalType.MergedLeft
            };
            compound.SolveConditionalExpression();
                    
            // Migrate Condition to instruction
            b.Last = trueAssignment;
            trueAssignment.OriginalBlock = b.BlockId;
            trueAssignee.Identifier = destReg;
            trueAssignment.Right = compound;
            //ReplaceLogicalRight(trueAssignment, compound);
            trueAssignment.Absorb(jump);
            trueAssignment.Absorb(trueAssignment);
            trueAssignment.Absorb(trueCondition);
            trueAssignment.Absorb(alternateAssignment);
                    
            // See if we can merge with the follow
            edgeTrue.ClearSuccessors();
            trueTrue.ClearSuccessors();
            trueFalse.ClearSuccessors();
            b.ClearSuccessors();

            if (follow.Predecessors.Count == 0)
            {
                if (follow.PhiFunctions.TryGetValue(destReg.RegNum, out var phi2))
                {
                    trueAssignee.Identifier = phi2.Left;
                    follow.PhiFunctions.Remove(destReg.RegNum);
                }
                
                b.StealSuccessors(follow);
                b.AbsorbInstructions(follow, true);
                f.RemoveBlockAt(follow.BlockIndex);
            }
            else
            {
                b.AddSuccessor(follow);
            }

            f.RemoveAllBlocks(block => block == trueTrue || block == edgeTrue || block == trueFalse);

            return true;
        }

        return false;
    }

    private static bool MergeShortCircuitPattern(Function f, BasicBlock b, IdentifierDefinitionUseAnalyzer useAnalysis)
    {
        // The other "short circuit" pattern you will see is just a simple if with no else
        if (b is
            {
                HasInstructions: true,
                Last: ConditionalJump jump,
                EdgeTrue:
                {
                    Predecessors.Count: 1,
                    Successors: [{ } follow],
                    Instructions.Count: 1,
                    First: Assignment
                    {
                        IsSingleAssignment: true,
                        Left: IdentifierReference { Identifier.IsRegister: true } trueAssignee,
                        Right: not null
                    } trueAssignment
                } edgeTrue,
                EdgeFalse:
                {
                    Predecessors.Count: >= 2,
                } edgeFalse
            } && follow == edgeFalse)
        {
            bool isOr = false;
            var rightExp = LogicalRight(jump.Condition);
            if (rightExp is UnaryOp { Operation: UnaryOp.OperationType.OpNot, IsImplicit: true } op)
            {
                isOr = true;
                rightExp = op.Expression;
            }

            if (rightExp is not IdentifierReference ir ||
                ir.Identifier.RegNum != trueAssignee.Identifier.RegNum)
            {
                return false;
            }
                    
            var destReg = trueAssignee.Identifier;
                    
            if (follow.PhiFunctions.TryGetValue(destReg.RegNum, out var phi))
            {
                if (!phi.Right.Contains(trueAssignee.Identifier) || !phi.Right.Contains(ir.Identifier))
                    return false;
                
                if ((phi.Right.Count == 2 && phi.Right[0] == trueAssignee.Identifier &&
                     phi.Right[1] == ir.Identifier) || 
                    (phi.Right.Count == 2 && phi.Right[1] == trueAssignee.Identifier &&
                     phi.Right[0] == ir.Identifier))
                {
                    // If there's only two phi inputs we can remove them and the phi function
                    follow.PhiFunctions.Remove(destReg.RegNum);
                    destReg = phi.Left;
                }
                else
                {
                    // Otherwise just remove the phi for the jump
                    phi.Right.Remove(ir.Identifier);
                }
            }
            else if (useAnalysis.UseCount(trueAssignee.Identifier) != 0 || 
                     useAnalysis.UseCount(ir.Identifier) != 0)
            {
                return false;
            }
                    
            // Build compound condition
            NotLogicalRight(jump);
            var compound = new BinOp(jump.Condition, trueAssignment.Right,
                isOr ? BinOp.OperationType.OpOr : BinOp.OperationType.OpAnd)
            {
                MergedCompoundConditional = BinOp.MergedCompoundConditionalType.MergedLeft
            };
            compound.SolveConditionalExpression();
                    
            // Migrate Condition to instruction
            b.Last = trueAssignment;
            trueAssignment.OriginalBlock = b.BlockId;
            trueAssignee.Identifier = destReg;
            trueAssignment.Right = compound;
            trueAssignment.Absorb(jump);
                    
            // See if we can merge with the follow
            edgeTrue.ClearSuccessors();
            b.ClearSuccessors();

            if (follow.Predecessors.Count == 0)
            {
                if (follow.PhiFunctions.TryGetValue(destReg.RegNum, out var phi2))
                {
                    trueAssignee.Identifier = phi2.Left;
                    follow.PhiFunctions.Remove(destReg.RegNum);
                }
                        
                b.StealSuccessors(follow);
                b.AbsorbInstructions(follow, true);
                f.RemoveBlockAt(follow.BlockIndex);
            }
            else
            {
                b.AddSuccessor(follow);
            }
            f.RemoveBlockAt(edgeTrue.BlockIndex);
            
            return true;
        }

        return false;
    }
    
    public bool RunOnFunction(DecompilationContext decompilationContext, FunctionContext functionContext, Function f)
    {
        var useAnalysis = functionContext.GetAnalysis<IdentifierDefinitionUseAnalyzer>();
        
        var irChanged = false;
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var b in f.BlockList)
            {
                // First possible pattern is a full if else block that will assign to the right side of a condition
                // chain
                if (MergeBasicPattern(f, b, useAnalysis) || MergeChainPattern(f, b, useAnalysis) || 
                    MergeChainPattern2(f, b, useAnalysis) || MergeShortCircuitPattern(f, b, useAnalysis))
                {
                    changed = true;
                    irChanged = true;
                    break;
                }
            }
        }
        
        if (irChanged)
        {
            functionContext.InvalidateAnalysis<DominanceAnalyzer>();
            functionContext.InvalidateAnalysis<IdentifierDefinitionUseAnalyzer>();
            functionContext.InvalidateAnalysis<LocalVariablesAnalyzer>();
        }

        return irChanged;
    }
}