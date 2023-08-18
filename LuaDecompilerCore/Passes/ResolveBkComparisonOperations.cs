using System;
using System.Collections.Generic;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Hks has "bk" versions of its comparison ops (LT_BK and LE_BK) which are similar to LT and LE except that the left
/// side of the comparison is always a constant and the right side is a register, while with the normal comparison ops
/// the left is always a register and the right can be either a register or a constant. This is run at the end when
/// all register values are substituted, but we still need to do some work to ensure that all comparison operations
/// that came from an RK comparison have a constant on the left side (after flipping so that the op is '<' or '<=') and
/// the ones that don't do not have a constant on the left.
///
/// This is done by factoring out implicit "not" operations that likely existed in the source but the compiler folded
/// into the expression.
/// </summary>
public class ResolveBkComparisonOperations : IPass
{
    private List<UnaryOp> _notStack = new();

    private BinOp? NextBinOp(Expression expression)
    {
        while (expression is UnaryOp op)
        {
            if (op.Operation is not UnaryOp.OperationType.OpNot)
                return null;
            expression = op.Expression;
        }

        return expression as BinOp;
    }
    
    private void ProcessExpression(ref Expression parent, Expression expression, bool flip)
    {
        // See if we are a compound and
        if (expression is BinOp { Operation: BinOp.OperationType.OpAnd } andOp)
        {
            var left = NextBinOp(andOp.Left);
            var right = NextBinOp(andOp.Right);
            
            // If the left needs to be flipped and the right cannot be flipped or vice versa, then we can't resolve this
            if (left is { IsNonEqualityComparison: true, IsBkLegal: var leftBkLegal } &&
                right is { IsNonEqualityComparison: true, IsBkLegal: var rightBkLegal } &&
                leftBkLegal != rightBkLegal)
            {
                throw new Exception("and comparison where one but not both child comparisons are legal");
            }
            
            
        }
    }
    
    public bool RunOnFunction(DecompilationContext decompilationContext, FunctionContext functionContext, Function f)
    {
        foreach (var block in f.BlockList)
        {
            foreach (var instruction in block.Instructions)
            {
                
            }
        }

        return false;
    }
}