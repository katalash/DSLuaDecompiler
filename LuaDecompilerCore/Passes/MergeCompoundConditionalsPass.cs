using System;
using System.Diagnostics;
using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Merge conditionals that follow short circuit patterns into compound conditionals (like "if a and b")
/// </summary>
public class MergeCompoundConditionalsPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            f.NumberReversePostorder();
            foreach (var node in f.PostorderTraversal(false))
            {
                if (node.Instructions.Count > 0 && node.Instructions.Last() is Jump 
                    { 
                        Conditional: true, 
                        Condition: BinOp 
                        {
                            Operation: BinOp.OperationType.OpLoopCompare
                        }
                    })
                {
                    continue;
                }
                if (node.Successors.Count == 2 && node.Instructions.Last() is Jump { Condition: not null } n)
                {
                    var t = node.Successors[0];
                    var e = node.Successors[1];
                    if (t.Successors.Count == 2 && t.Instructions.First() is Jump { Condition: not null } tj && 
                        t.Predecessors.Count == 1)
                    {
                        if (t.Successors[0] == e && t.Successors[1] != e)
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
                            node.Successors[1] = t.Successors[1];
                            n.BlockDest = node.Successors[1];
                            var i = t.Successors[1].Predecessors.IndexOf(t);
                            t.Successors[1].Predecessors[i] = node;
                            node.Successors[0] = e;
                            i = t.Successors[0].Predecessors.IndexOf(t);
                            //e.Predecessors[i] = node;
                            f.BlockList.Remove(t);
                            e.Predecessors.Remove(t);
                            t.Successors[1].Predecessors.Remove(t);
                            changed = true;
                        }
                        else if (t.Successors[1] == e)
                        {
                            var newCond = new BinOp(n.Condition, tj.Condition, BinOp.OperationType.OpAnd);
                            n.Condition = newCond;
                            if (t.Follow != null)
                            {
                                Debug.Assert(node.Follow != null);
                                node.Follow = node.Follow.ReversePostorderNumber > t.Follow.ReversePostorderNumber ? 
                                    node.Follow : t.Follow;
                            }
                            node.Successors[0] = t.Successors[0];
                            var i = t.Successors[0].Predecessors.IndexOf(t);
                            t.Successors[0].Predecessors[i] = node;
                            e.Predecessors.Remove(t);
                            f.BlockList.Remove(t);
                            changed = true;
                        }
                    }
                    else if (e.Successors.Count == 2 && e.Instructions.First() is Jump { Condition: not null } ej && 
                             e.Predecessors.Count == 1)
                    {
                        if (e.Successors[0] == t)
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
                            node.Successors[1] = e.Successors[1];
                            n.BlockDest = node.Successors[1];
                            var i = e.Successors[1].Predecessors.IndexOf(e);
                            e.Successors[1].Predecessors[i] = node;
                            t.Predecessors.Remove(e);
                           f. BlockList.Remove(e);
                            changed = true;
                        }
                        else if (e.Successors[1] == t)
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
                            node.Successors[1] = e.Successors[0];
                            var i = e.Successors[0].Predecessors.IndexOf(e);
                            e.Successors[0].Predecessors[i] = node;
                            t.Predecessors.Remove(e);
                            f.BlockList.Remove(e);
                            changed = true;
#endif
                        }
                    }
                }
            }
        }
        f.ComputeDominance();
    }
}