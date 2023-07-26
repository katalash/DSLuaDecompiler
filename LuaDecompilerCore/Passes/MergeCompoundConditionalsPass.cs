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
    public void RunOnFunction(DecompilationContext decompilationContext, FunctionContext functionContext, Function f)
    {
        bool changed = true;
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
                            //var newCond = new BinOp(new UnaryOp(n.Condition, UnaryOp.OperationType.OpNot), tj.Condition, BinOp.OperationType.OpOr);
                            Expression newCond;
                            if (n.Condition is BinOp { IsCompare: true } b)
                            {
                                newCond = new BinOp(b.NegateCondition(), tj.Condition, BinOp.OperationType.OpOr);
                            }
                            else
                            {
                                newCond = new BinOp(new UnaryOp(n.Condition, UnaryOp.OperationType.OpNot), 
                                    tj.Condition, BinOp.OperationType.OpOr);
                            }
                            n.Condition = newCond;
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
                            changed = true;
                        }
                        else if (t.EdgeFalse == e)
                        {
                            var newCond = new BinOp(n.Condition, tj.Condition, BinOp.OperationType.OpAnd);
                            n.Condition = newCond;
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
                            changed = true;
                        }
                    }
                    else if (e is { IsConditionalJump: true, First: ConditionalJump ej, Predecessors.Count: 1 })
                    {
                        if (e.EdgeTrue == t)
                        {
                            var newCond = new BinOp(new UnaryOp(n.Condition, UnaryOp.OperationType.OpNot), 
                                ej.Condition, BinOp.OperationType.OpOr);
                            n.Condition = newCond;
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
    }
}