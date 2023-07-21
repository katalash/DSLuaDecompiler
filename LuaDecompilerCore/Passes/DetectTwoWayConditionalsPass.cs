using System;
using System.Collections.Generic;
using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Detects and labels two way statements (if statements)
/// </summary>
public class DetectTwoWayConditionalsPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        var debugVisited = new HashSet<CFG.BasicBlock>();
        HashSet<CFG.BasicBlock> Visit(CFG.BasicBlock b)
        {
            var unresolved = new HashSet<CFG.BasicBlock>();
            foreach (var successor in b.DominanceTreeSuccessors)
            {
                if (debugVisited.Contains(successor))
                {
                    throw new Exception("Revisited dom tree node " + successor);
                }
                debugVisited.Add(successor);
                unresolved.UnionWith(Visit(successor));
            }

            if (b.IsConditional && (!b.IsLoopHead || b.LoopType != CFG.LoopType.LoopPretested))
            {
                var maxEdges = 0;
                CFG.BasicBlock? maxNode = null;
                foreach (var d in b.DominanceTreeSuccessors)
                {
                    var successorsReq = 2;
                    // If there is a break or while, the follow node is only going to have one back-edge
                    if (b.LoopBreakFollow != null || b.LoopContinueFollow != null)
                    {
                        successorsReq = 1;
                    }
                    if (d.Predecessors.Count >= successorsReq && d.Predecessors.Count > maxEdges && 
                        !d.IsContinueNode && !d.IsBreakNode && d != f.EndBlock)
                    {
                        maxEdges = d.Predecessors.Count;
                        maxNode = d;
                    }
                }
                // Heuristic: if the true branch leads to a return or is if-orphaned and the follow isn't defined already, then the follow is always the false branch
                // If the true branch also has a follow chain defined that leads to a return or if-orphaned node, then it is also disjoint from the rest of the CFG
                // and the false branch is the follow
                var isDisjoint = false;
                var testFollow = b.EdgeTrue.Follow;
                while (testFollow != null)
                {
                    if (testFollow.IsReturn || testFollow.IfOrphaned)
                    {
                        isDisjoint = true;
                        break;
                    }
                    testFollow = testFollow.Follow;
                }
                if (maxNode == null && (b.EdgeTrue.IsReturn || b.EdgeTrue.IfOrphaned || isDisjoint))
                {
                    // If the false branch leads to an isolated return node or an if-orphaned node, then we are if-orphaned, which essentially means we don't
                    // have a follow defined in the CFG. This means that to structure this, the if-orphaned node must be adopted by the next node with a CFG
                    // determined follow and this node will inherit that follow
                    if (b.EdgeFalse is { IsReturn: true, Predecessors.Count: 1 } || b.EdgeFalse.IfOrphaned)
                    {
                        b.IfOrphaned = true;
                    }
                    else
                    {
                        maxNode = b.EdgeFalse;
                    }
                }
                // If you don't match anything, but you dominate the end node, then it's probably the follow
                if (maxNode == null && b.DominanceTreeSuccessors.Contains(f.EndBlock))
                {
                    maxNode = f.EndBlock;
                }

                // If we are a latch and the false node leads to a loop head, then the follow is the loop head
                if (maxNode == null && b is { IsLoopLatch: true, EdgeFalse.IsLoopHead: true })
                {
                    maxNode = b.EdgeFalse;
                }

                if (maxNode != null)
                {
                    b.Follow = maxNode;
                    var unresolvedClone = new HashSet<CFG.BasicBlock>(unresolved);
                    foreach (var x in unresolvedClone)
                    {
                        if (x != maxNode && !x.Dominance.Contains(maxNode))
                        {
                            var inc = x.DominanceTreeSuccessors.Count == 0;
                            // Do a BFS down the dominance hierarchy to search for a follow
                            var bfsQueue = new Queue<CFG.BasicBlock>(x.DominanceTreeSuccessors);
                            //foreach (var domsucc in x.DominanceTreeSuccessors)
                            //{
                            while (bfsQueue.Count > 0)
                            {
                                var dominanceSuccessor = bfsQueue.Dequeue();
                                if (dominanceSuccessor.Successors.Contains(maxNode) || dominanceSuccessor.Follow == maxNode)
                                {
                                    inc = true;
                                    break;
                                }
                                dominanceSuccessor.DominanceTreeSuccessors.ForEach(s => bfsQueue.Enqueue(s));
                            }
                            //}
                            if (x.IfOrphaned)
                            {
                                inc = true;
                            }
                            if (inc)
                            {
                                x.Follow = maxNode;
                                unresolved.Remove(x);
                            }
                        }
                    }

                }
                else
                {
                    unresolved.Add(b);
                }
            }

            // The loop head or latch is the implicit follow of any unmatched conditionals
            if (b.IsLoopHead)
            {
                foreach (var ur in unresolved)
                {
                    // If there's a single loop latch and it has multiple predecessors, it's probably the follow
                    if (b.LoopLatches is [{ Predecessors.Count: > 1 }])
                    {
                        ur.Follow = b.LoopLatches[0];
                    }
                    // Otherwise the detected latch (of multiple) is probably within an if statement and the head is the true follow
                    else
                    {
                        ur.Follow = b;
                    }
                }
                unresolved.Clear();
            }

            return unresolved;
        }

        // Unsure about this logic, but the idea is that an if chain at the end that only returns will be left unmatched and unadopted,
        // and thus the follows need to be the end blocks
        var unmatched = Visit(f.BeginBlock);
        foreach (var u in unmatched)
        {
            u.Follow = f.EndBlock;
        }
    }
}