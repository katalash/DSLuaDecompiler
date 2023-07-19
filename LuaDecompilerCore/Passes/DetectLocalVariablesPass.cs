using System.Collections.Generic;
using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Detects and labels declarations of local variables. These are the first definitions of variables
/// in a dominance hierarchy.
/// </summary>
public class DetectLocalVariablesPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        // This is kinda both a pre and post-order traversal of the dominance heirarchy. In the pre traversal,
        // first local definitions are detected, marked, and propogated down the graph so that they aren't marked
        // again. In the postorder traversal, these marked definitions are backpropogated up the dominance heirarchy.
        // If a node gets multiple marked nodes for the same variable from its children in the dominance heirarchy,
        // a new local assignment must be inserted right before the node splits.
        Dictionary<Identifier, List<Assignment>> Visit(CFG.BasicBlock b, HashSet<Identifier> declared)
        {
            var newDeclared = new HashSet<Identifier>(declared);
            var declaredAssignments = new Dictionary<Identifier, List<Assignment>>();

            // Go through the graph and mark declared nodes
            foreach (var inst in b.Instructions)
            {
                if (inst is Assignment { IsSingleAssignment: true } a)
                {
                    foreach (var def in a.GetDefines(true))
                    {
                        // If the definition has been renamed at this point then it's from a parent closure and should not be made a local
                        if (!def.Renamed && !newDeclared.Contains(def))
                        {
                            newDeclared.Add(def);
                            a.IsLocalDeclaration = true;
                            declaredAssignments.Add(def, new List<Assignment> { a });
                        }
                    }
                }
            }

            // Visit and merge the children in the dominance heirarchy
            var inherited = new Dictionary<Identifier, List<Assignment>>();
            var phiInduced = new HashSet<Identifier>();
            foreach (var successor in b.DominanceTreeSuccessors)
            {
                var cDeclared = Visit(successor, newDeclared);
                foreach (var entry in cDeclared)
                {
                    if (!inherited.ContainsKey(entry.Key))
                    {
                        inherited.Add(entry.Key, new List<Assignment>(entry.Value));
                    }
                    else
                    {
                        inherited[entry.Key].AddRange(entry.Value);
                    }
                }
                phiInduced.UnionWith(successor.PhiMerged);
            }
            foreach (var entry in inherited)
            {
                if (entry.Value.Count > 1 && phiInduced.Contains(entry.Key))
                {
                    // Multiple incoming declarations that all have the same use need to be merged
                    var assignment = new Assignment(entry.Key, null)
                    {
                        IsLocalDeclaration = true
                    };
                    b.Instructions.Insert(b.Instructions.Count - 1, assignment);
                    declaredAssignments.Add(entry.Key, new List<Assignment> { assignment });
                    foreach (var e in entry.Value)
                    {
                        e.IsLocalDeclaration = false;
                    }
                }
                else
                {
                    declaredAssignments.Add(entry.Key, entry.Value);
                }
            }

            return declaredAssignments;
        }

        Visit(f.BeginBlock, new HashSet<Identifier>(f.Parameters));
    }
}