using System;
using System.Collections.Generic;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Does global liveness analysis to verify no copies are needed coming out of SSA form
/// </summary>
public class ValidateLivenessNoInterferencePass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        // Just computes liveout despite the name
        f.ComputeGlobalLiveness(f.SsaVariables);

        var globalLiveness = new Dictionary<Identifier, HashSet<Identifier>>();
        // Initialise the disjoint sets
        foreach (var id in f.SsaVariables)
        {
            globalLiveness.Add(id, new HashSet<Identifier> { id });
        }

        // Do a super shitty unoptimal union find algorithm to merge all the global ranges using phi functions
        // Rewrite this with a proper union-find if performance becomes an issue (lol)
        foreach (var b in f.BlockList)
        {
            foreach (var phi in b.PhiFunctions.Values)
            {
                foreach (var r in phi.Right)
                {
                    if (phi.Left != null && r != null && globalLiveness[phi.Left] != globalLiveness[r])
                    {
                        globalLiveness[phi.Left].UnionWith(globalLiveness[r]);
                        globalLiveness[r] = globalLiveness[phi.Left];
                    }
                }
            }
        }

        foreach (var b in f.BlockList)
        {
            var liveNow = new HashSet<Identifier>(b.LiveOut);
            for (var i = b.Instructions.Count - 1; i >= 0; i--)
            {
                var defs = b.Instructions[i].GetDefines(true);
                foreach (var def in defs)
                {
                    foreach (var live in liveNow)
                    {
                        if (live != def && live.OriginalIdentifier == def.OriginalIdentifier)
                        {
                            Console.WriteLine($@"Warning: SSA live range interference detected in function {f.FunctionId}. Results are probably wrong.");
                        }
                    }
                    liveNow.Remove(def);
                }
                foreach (var use in b.Instructions[i].GetUses(true))
                {
                    liveNow.Add(use);
                }
            }
        }
    }
}