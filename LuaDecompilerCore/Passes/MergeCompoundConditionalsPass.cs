using System;
using System.Diagnostics;
using LuaDecompilerCore.Analyzers;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Merge conditionals that follow short circuit patterns into compound conditionals (like "if a and b")
/// </summary>
public class MergeCompoundConditionalsPass : IPass
{
    public bool MutatesCfg => true;
    public bool RunOnFunction(DecompilationContext decompilationContext, FunctionContext functionContext, Function f)
    {
        var irChanged = false;
        var changed = true;
        while (changed)
        {
            changed = false;
            f.NumberReversePostorder();
            foreach (var node in f.PostorderTraversal(false))
            {
                if (node is 
                    { 
                        HasInstructions: true, 
                        Last: ConditionalJump 
                        { 
                            Condition: BinOp 
                            {
                                Operation: BinOp.OperationType.OpLoopCompare
                            }
                        }
                    })
                {
                    continue;
                }
                if (node is
                    {
                        IsConditionalJump: true, 
                        Last: ConditionalJump n
                    })
                {
                    var t = node.EdgeTrue;
                    var e = node.EdgeFalse;
                    if (t is { IsConditionalJump: true, First: ConditionalJump tj, Predecessors.Count: 1 })
                    {
                        if (t.EdgeTrue == e && t.EdgeFalse != e)
                        {
                            Expression newCond;
                            if (n.Condition is BinOp { IsCompare: true } b)
                            {
                                b.NegateConditionalExpression();
                                newCond = new BinOp(b, tj.Condition, BinOp.OperationType.OpOr);
                            }
                            else
                            {
                                var left = n.Condition;
                                if (n.Condition is UnaryOp { Operation: UnaryOp.OperationType.OpNot } unaryOp)
                                    left = unaryOp.Expression;
                                else if (n.Condition is BinOp binOp)
                                    binOp.NegateConditionalExpression();
                                else
                                    left = new UnaryOp(n.Condition, UnaryOp.OperationType.OpNot);
                                newCond = new BinOp(left, tj.Condition, BinOp.OperationType.OpOr);
                            }
                            n.Condition = newCond;
                            n.OriginalBlock = tj.OriginalBlock;
                            n.Absorb(tj);
                            if (t.Follow != null)
                            {
                                Debug.Assert(node.Follow != null);
                                node.Follow = node.Follow.ReversePostorderNumber > t.Follow.ReversePostorderNumber ? 
                                    node.Follow : t.Follow;
                            }
                            node.EdgeFalse = t.EdgeFalse;
                            n.Destination = node.EdgeFalse;
                            var i = t.EdgeFalse.Predecessors.IndexOf(t);
                            t.EdgeFalse.Predecessors[i] = node;
                            node.EdgeTrue = e;
                            i = t.EdgeTrue.Predecessors.IndexOf(t);
                            //e.Predecessors[i] = node;
                            f.BlockList.Remove(t);
                            e.Predecessors.Remove(t);
                            t.EdgeFalse.Predecessors.Remove(t);
                            irChanged = true;
                            changed = true;
                        }
                        else if (t.EdgeFalse == e)
                        {
                            var newCond = new BinOp(n.Condition, tj.Condition, BinOp.OperationType.OpAnd);
                            n.Condition = newCond;
                            n.Absorb(tj);
                            n.OriginalBlock = tj.OriginalBlock;
                            if (t.Follow != null)
                            {
                                Debug.Assert(node.Follow != null);
                                node.Follow = node.Follow.ReversePostorderNumber > t.Follow.ReversePostorderNumber ? 
                                    node.Follow : t.Follow;
                            }
                            node.EdgeTrue = t.EdgeTrue;
                            var i = t.EdgeTrue.Predecessors.IndexOf(t);
                            t.EdgeTrue.Predecessors[i] = node;
                            e.Predecessors.Remove(t);
                            f.BlockList.Remove(t);
                            irChanged = true;
                            changed = true;
                        }
                    }
                    else if (e is { IsConditionalJump: true, First: ConditionalJump ej, Predecessors.Count: 1 })
                    {
                        if (e.EdgeTrue == t)
                        {
                            var left = n.Condition;
                            if (n.Condition is UnaryOp { Operation: UnaryOp.OperationType.OpNot } unaryOp)
                                left = unaryOp.Expression;
                            else if (n.Condition is BinOp binOp)
                                binOp.NegateConditionalExpression();
                            else
                                left = new UnaryOp(n.Condition, UnaryOp.OperationType.OpNot);
                            var newCond = new BinOp(left, ej.Condition, BinOp.OperationType.OpOr);
                            n.Condition = newCond;
                            n.Absorb(ej);
                            n.OriginalBlock = ej.OriginalBlock;
                            if (e.Follow != null)
                            {
                                Debug.Assert(node.Follow != null);
                                node.Follow = node.Follow.ReversePostorderNumber > e.Follow.ReversePostorderNumber ? 
                                    node.Follow : e.Follow;
                            }
                            node.EdgeFalse = e.EdgeFalse;
                            n.Destination = node.EdgeFalse;
                            var i = e.EdgeFalse.Predecessors.IndexOf(e);
                            e.EdgeFalse.Predecessors[i] = node;
                            t.Predecessors.Remove(e);
                            f.BlockList.Remove(e);
                            irChanged = true;
                            changed = true;
                        }
                        else if (e.EdgeFalse == t)
                        {
                            // TODO: not correct
                            throw new Exception("this is used so fix it");
#if false
                            var newCond = new BinOp(n.Condition, ej.Condition, BinOp.OperationType.OpOr);
                            n.Condition = newCond;
                            if (e.Follow != null)
                            {
                                node.Follow = node.Follow.ReversePostorderNumber > e.Follow.ReversePostorderNumber ? node.Follow : e.Follow;
                            }
                            node.EdgeFalse = e.EdgeTrue;
                            var i = e.EdgeTrue.Predecessors.IndexOf(e);
                            e.EdgeTrue.Predecessors[i] = node;
                            t.Predecessors.Remove(e);
                            f.BlockList.Remove(e);
                            changed = true;
#endif
                        }
                    }
                }
            }
        }
        functionContext.InvalidateAnalysis<DominanceAnalyzer>();

        return irChanged;
    }
}