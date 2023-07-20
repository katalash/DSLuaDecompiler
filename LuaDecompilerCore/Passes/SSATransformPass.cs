using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Converts the function representation to single-static analysis form:
/// * each register is renamed such that values are only assigned once
/// * convergence is handled with phi nodes
/// </summary>
public class SsaTransformPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        var allRegisters = f.GetAllRegisters();
        allRegisters.UnionWith(new HashSet<Identifier>(f.Parameters));
        f.ComputeGlobalLiveness(allRegisters);

        // Now insert all the needed phi functions
        foreach (var g in f.GlobalIdentifiers)
        {
            var work = new Queue<CFG.BasicBlock>();
            var visitedSet = new HashSet<CFG.BasicBlock>();
            foreach (var b in f.BlockList)
            {
                if (b != f.EndBlock && b.KilledIdentifiers.Contains(g))
                {
                    work.Enqueue(b);
                    visitedSet.Add(b);
                }
            }
            while (work.Count > 0)
            {
                var b = work.Dequeue();
                foreach (var d in b.DominanceFrontier)
                {
                    if (d != f.EndBlock && !d.PhiFunctions.ContainsKey(g))
                    {
                        // Heuristic: if the block is just a single return, we don't need phi functions
                        if (d.Instructions.First() is Return r && r.ReturnExpressions.Count == 0)
                        {
                            continue;
                        }

                        var phiArgs = new List<Identifier?>();
                        for (var i = 0; i < d.Predecessors.Count; i++)
                        {
                            phiArgs.Add(g);
                        }
                        d.PhiFunctions.Add(g, new PhiFunction(g, phiArgs));
                        //if (!visitedSet.Contains(d))
                        //{
                        work.Enqueue(d);
                        visitedSet.Add(d);
                        //}
                    }
                }
            }
        }

        // Prepare for renaming
        var counters = new Dictionary<Identifier, int>();
        var stacks = new Dictionary<Identifier, Stack<Identifier>>();
        foreach (var reg in allRegisters)
        {
            counters.Add(reg, 0);
            stacks.Add(reg, new Stack<Identifier>());
        }

        // Creates a new identifier based on an original identifier
        Identifier NewName(Identifier orig)
        {
            var newName = new Identifier
            {
                Name = orig.Name + $@"_{counters[orig]}",
                Type = Identifier.IdentifierType.Register,
                OriginalIdentifier = orig,
                IsClosureBound = orig.IsClosureBound
            };
            stacks[orig].Push(newName);
            counters[orig]++;
            f.SsaVariables.Add(newName);
            return newName;
        }

        void RenameBlock(CFG.BasicBlock b)
        {
            // Rewrite phi function definitions
            foreach (var phi in b.PhiFunctions)
            {
                phi.Value.RenameDefines(phi.Key, NewName(phi.Key));
            }

            // Rename other instructions
            foreach (var inst in b.Instructions)
            {
                foreach (var use in inst.GetUses(true))
                {
                    if (use.IsClosureBound)
                    {
                        continue;
                    }
                    if (stacks[use].Count != 0)
                    {
                        inst.RenameUses(use, stacks[use].Peek());
                    }
                }
                foreach (var def in inst.GetDefines(true))
                {
                    if (def.IsClosureBound)
                    {
                        continue;
                    }
                    inst.RenameDefines(def, NewName(def));
                }
            }
                
            // Rename successor phi functions
            foreach (var successor in b.Successors)
            {
                if (successor == f.EndBlock) continue;
                var index = successor.Predecessors.IndexOf(b);
                foreach (var phi in successor.PhiFunctions)
                {
                    if (phi.Value.Right[index] is { } k && stacks[k].Count > 0)
                    {
                        phi.Value.Right[index] = stacks[k].Peek();
                        stacks[k].Peek().UseCount++;
                    }
                    else
                    {
                        // Sometimes a phi function is forced when one of the predecessor paths don't actually define the register.
                        // These phi functions are usually not needed and optimized out in a later pass, so we set it to null to detect
                        // errors in case the phi function result is actually used.
                        phi.Value.Right[index] = null;
                    }
                }
            }
                
            // Rename successors in the dominator tree
            foreach (var successor in b.DominanceTreeSuccessors)
            {
                if (successor != f.EndBlock)
                {
                    RenameBlock(successor);
                }

                // Add to the scope killed set based on the domtree successor's killed and scope killed
                foreach (var killed in successor.KilledIdentifiers)
                {
                    if (killed.Type == Identifier.IdentifierType.Register)
                    {
                        b.ScopeKilled.Add(killed.RegNum);
                    }
                    b.ScopeKilled.UnionWith(successor.ScopeKilled);
                }
            }

            // Pop off anything we pushed
            foreach (var phi in b.PhiFunctions)
            {
                Debug.Assert(phi.Value.Left.OriginalIdentifier != null);
                stacks[phi.Value.Left.OriginalIdentifier].Pop();
            }
            foreach (var inst in b.Instructions)
            {
                foreach (var def in inst.GetDefines(true))
                {
                    if (def.IsClosureBound)
                    {
                        continue;
                    }
                    Debug.Assert(def.OriginalIdentifier != null);
                    stacks[def.OriginalIdentifier].Pop();
                }
            }
        }

        // Rename the arguments first
        for (var i = 0; i < f.Parameters.Count; i++)
        {
            f.Parameters[i] = NewName(f.Parameters[i]);
        }

        // Rename everything else recursively
        RenameBlock(f.BeginBlock);
    }
}