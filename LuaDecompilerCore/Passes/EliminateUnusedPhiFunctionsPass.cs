using System.Collections.Generic;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Eliminate phi functions that don't end up being used at all by actual code
/// </summary>
public class EliminateUnusedPhiFunctionsPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        var phisToKeep = new HashSet<PhiFunction>();
        var usedIdentifiers = new HashSet<Identifier>();

        // First, iterate through all the non-phi instructions to get all the used identifiers
        foreach (var b in f.BlockList)
        {
            foreach (var inst in b.Instructions)
            {
                foreach (var use in inst.GetUses(true))
                {
                    if (!usedIdentifiers.Contains(use))
                    {
                        usedIdentifiers.Add(use);
                    }
                }
            }
        }

        // Next do an expansion cycle where phi functions that use the identifiers are marked kept, and then phi functions that the marked phi uses are also kept
        bool changed = false;
        foreach (var b in f.BlockList)
        {
            foreach (var phi in b.PhiFunctions)
            {
                if (!phisToKeep.Contains(phi.Value) && usedIdentifiers.Contains(phi.Value.Left))
                {
                    phisToKeep.Add(phi.Value);
                    foreach (var use in phi.Value.Right)
                    {
                        if (!usedIdentifiers.Contains(use))
                        {
                            usedIdentifiers.Add(use);
                        }
                    }
                    changed = true;
                }
            }
        }

        // Now prune any phi functions that aren't marked
        foreach (var b in f.BlockList)
        {
            var phiToRemove = new List<Identifier>();
            foreach (var phi in b.PhiFunctions)
            {
                if (!phisToKeep.Contains(phi.Value))
                {
                    foreach (var i in phi.Value.Right)
                    {
                        if (i != null)
                        {
                            i.UseCount--;
                        }
                    }
                    phiToRemove.Add(phi.Key);
                }
            }
            phiToRemove.ForEach(x => b.PhiFunctions.Remove(x));
        }
    }
}