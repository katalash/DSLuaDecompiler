using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Drop SSA by simply dropping the subscripts. This requires no interference in the live ranges of the definitions
/// </summary>
public class DropSsaSubscriptsPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        foreach (var b in f.BlockList)
        {
            foreach (var phi in b.PhiFunctions)
            {
                Debug.Assert(phi.Value.Left.OriginalIdentifier != null);
                b.PhiMerged.Add(phi.Value.Left.OriginalIdentifier);
            }
            b.PhiFunctions.Clear();
            foreach (var i in b.Instructions)
            {
                foreach (var def in i.GetDefines(true))
                {
                    if (def.OriginalIdentifier != null)
                        i.RenameDefines(def, def.OriginalIdentifier);
                }
                foreach (var use in i.GetUses(true))
                {
                    if (use.OriginalIdentifier != null)
                        i.RenameUses(use, use.OriginalIdentifier);
                }
            }
        }
        for (var a = 0; a < f.Parameters.Count; a++)
        {
            if (f.Parameters[a].OriginalIdentifier is { } identifier)
                f.Parameters[a] = identifier;
        }

        var counter = 0;
        Identifier NewName(Identifier orig)
        {
            var newName = new Identifier
            {
                Name = orig.Name + $@"_{counter}",
                Type = Identifier.IdentifierType.Register,
                OriginalIdentifier = orig
            };
            counter++;
            return newName;
        }

        // If we have debug information, we can split up variables based on if a definition is associated with the start
        // of a local variable. If so, everything dominated by the containing block is renamed to that definition
        void Visit(CFG.BasicBlock b, Dictionary<Identifier, Identifier> replacements)
        {
            var newReplacements = new Dictionary<Identifier, Identifier>(replacements);

            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var phi in b.PhiMerged.ToList())
                {
                    if (newReplacements.TryGetValue(phi, out var replacement))
                    {
                        b.PhiMerged.Remove(phi);
                        b.PhiMerged.Add(replacement);
                        changed = true;
                    }
                }
            }
            // Walk down all the instructions, replacing things that need to be replaced and renaming as needed
            foreach (var instruction in b.Instructions)
            {
                changed = true;
                var reassigned = false;
                Identifier? newDef = null;
                while (changed)
                {
                    changed = false;
                    foreach (var use in instruction.GetUses(true))
                    {
                        if (newReplacements.TryGetValue(use, out var value) && newReplacements[use] != newDef)
                        {
                            instruction.RenameUses(use, value);
                            changed = true;
                        }
                    }
                    foreach (var def in instruction.GetDefines(true))
                    {
                        if (instruction is Assignment { LocalAssignments: not null } && !reassigned)
                        {
                            var newName = NewName(def);
                            instruction.RenameDefines(def, newName);
                            if (newReplacements.ContainsKey(def))
                            {
                                newReplacements[def] = newName;
                                newDef = newName;
                            }
                            else
                            {
                                newReplacements.Add(def, newName);
                                newDef = newName;
                            }
                            changed = true;
                            reassigned = true;
                        }
                        else if (newReplacements.TryGetValue(def, out var replacement))
                        {
                            instruction.RenameDefines(def, replacement);
                            changed = true;
                        }
                    }
                }
            }

            // Propagate to children in the dominance hierarchy
            foreach (var successor in b.DominanceTreeSuccessors)
            {
                Visit(successor, newReplacements);
            }
        }
        Visit(f.BeginBlock, new Dictionary<Identifier, Identifier>());
    }
}