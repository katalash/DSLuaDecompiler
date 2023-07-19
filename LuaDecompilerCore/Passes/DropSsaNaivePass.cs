using System.Collections.Generic;
using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Naive method to convert out of SSA. Not guaranteed to produce correct code since no liveness/interferance analysis is done.
/// This pass is no longer used because it actually sucked
/// </summary>
public class DropSsaNaivePass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        // Do a postorder traversal down the CFG and use the phi functions to create a map of renamings
        HashSet<CFG.BasicBlock> visited = new HashSet<CFG.BasicBlock>();
        HashSet<CFG.BasicBlock> processed = new HashSet<CFG.BasicBlock>();
        var remapCache = new Dictionary<CFG.BasicBlock, Dictionary<Identifier, Identifier>>();

        // This is used to propogate replacements induced by a loop latch down a dominance heirarchy 
        void BackPropagate(CFG.BasicBlock b, Dictionary<Identifier, Identifier> inReplacements)
        {
            // Rename variables in the block by traversing in reverse order
            for (int i = b.Instructions.Count - 1; i >= 0; i--)
            {
                var inst = b.Instructions[i];
                var defs = inst.GetDefines(true);
                foreach (var def in defs)
                {
                    if (inReplacements.TryGetValue(def, out var value))
                    {
                        inst.RenameDefines(def, value);
                        inReplacements.Remove(def);
                    }
                }
                foreach (var use in inst.GetUses(true))
                {
                    if (inReplacements.TryGetValue(use, out var replacement))
                    {
                        inst.RenameUses(use, replacement);
                    }
                }
            }

            foreach (var succ in b.DominanceTreeSuccessors)
            {
                BackPropagate(succ, inReplacements);
            }
        }

        var globalRenames = new Dictionary<Identifier, Identifier>();
        var globalRenamesInv = new Dictionary<Identifier, Identifier>();

        Dictionary<Identifier, Identifier> Visit(CFG.BasicBlock b)
        {
            visited.Add(b);
            // A set of mappings to rename variables induced by phi functions
            var replacements = new Dictionary<Identifier, Identifier>();
            foreach (var successors in b.Successors)
            {
                Dictionary<Identifier, Identifier>? preVisited = null;
                if (!visited.Contains(successors))
                {
                    preVisited = Visit(successors);
                }
                else
                {
                    if (remapCache.TryGetValue(successors, out var value))
                    {
                        preVisited = value;
                    }
                }
                if (preVisited != null)
                {
                    foreach (var rep in preVisited)
                    {
                        replacements.TryAdd(rep.Key, rep.Value);
                    }
                }
            }


            // First rename and delete phi functions by renaming the arguments to the assignment
            var phiUses = new HashSet<Identifier>();
            foreach (var phi in b.PhiFunctions)
            {
                // If the def is renamed by a later instruction, go ahead and rename it
                if (replacements.ContainsKey(phi.Value.Left))
                {
                    phi.Value.Left = replacements[phi.Value.Left];
                }
                var def = phi.Value.Left;
                foreach (var use in phi.Value.Right)
                {
                    if (use == null) 
                        continue;
                    
                    phiUses.Add(use);
                    if (replacements.TryGetValue(use, out var replacement))
                    {
                        if (replacement != def)
                        {
                            //throw new Exception("Conflicting phi function renames live at the same time");
                            /*if (!globalRenamesInv.ContainsKey(replacements[use]))
                            {
                                globalRenames.Add(replacements[use], def);
                                globalRenamesInv.Add(def, replacements[use]);
                            }
                            else
                            {
                                globalRenames[globalRenamesInv[replacements[use]]] = def;
                                globalRenamesInv.Add(def, globalRenamesInv[replacements[use]]);
                                globalRenamesInv.Remove(replacements[use]);
                            }
                            replacements[use] = def;*/
                        }
                    }
                    else
                    {
                        replacements.Add(use, def);
                    }
                }
            }
            b.PhiFunctions.Clear();

            // Rename variables in the block by traversing in reverse order
            for (int i = b.Instructions.Count - 1; i >= 0; i--)
            {
                var inst = b.Instructions[i];
                var defs = inst.GetDefines(true);
                foreach (var def in defs)
                {
                    if (replacements.ContainsKey(def))
                    {
                        inst.RenameDefines(def, replacements[def]);
                        // Only retire this replacement if it wasn't used by a phi function in this block
                        if (!phiUses.Contains(def))
                        {
                            replacements.Remove(def);
                        }
                    }
                }
                foreach (var use in inst.GetUses(true))
                {
                    if (replacements.TryGetValue(use, out var replacement))
                    {
                        inst.RenameUses(use, replacement);
                    }
                }
            }
            processed.Add(b);

            // If we are the first block, rename the function arguments
            if (b == f.BeginBlock)
            {
                for (int a = 0; a < f.Parameters.Count; a++)
                {
                    if (replacements.ContainsKey(f.Parameters[a]))
                    {
                        f.Parameters[a] = replacements[f.Parameters[a]];
                    }
                }
            }

            // Propogate the replacements to children if this is a latch (i.e. induces a loop) and the head was already processed
            foreach (var succ in b.Successors)
            {
                if (processed.Contains(succ) && succ.IsLoopHead)
                {
                    BackPropagate(succ, replacements);
                }
            }

            remapCache.Add(b, replacements);
            return replacements;
        }

        Visit(f.BeginBlock);

        // Go through all blocks/instructions and do the remaining renames
        foreach (var b in f.BlockList)
        {
            foreach (var i in b.Instructions)
            {
                foreach (var use in i.GetUses(true))
                {
                    if (globalRenames.TryGetValue(use, out var rename))
                    {
                        i.RenameUses(use, rename);
                    }
                }
                foreach (var use in i.GetDefines(true))
                {
                    if (globalRenames.TryGetValue(use, out var rename))
                    {
                        i.RenameDefines(use, rename);
                    }
                }
            }
        }
    }
}