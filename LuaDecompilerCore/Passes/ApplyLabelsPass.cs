using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Insert created labels into the instruction list
/// </summary>
public class ApplyLabelsPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        // O(n^2) naive algorithm but it hasn't been a problem yet
        foreach (var l in f.Labels)
        {
            foreach (var b in f.BlockList)
            {
                for (int i = 0; i < b.Instructions.Count; i++)
                {
                    if (b.Instructions[i].OpLocation == l.Key)
                    {
                        b.Instructions.Insert(i, l.Value);
                        break;
                    }
                }
            }
        }

        // Mark the implicit return lua always generates
        if (f.EndBlock.Instructions.Last() is Return r && r.ReturnExpressions.Count == 0)
        {
            r.IsImplicit = true;
        }
    }
}