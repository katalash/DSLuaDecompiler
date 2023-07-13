using System.Collections.Generic;
using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Detects list initializers as a series of statements that serially add data to a newly initialized list
/// </summary>
public class DetectGenericListInitializersPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        foreach (var b in f.BlockList)
        {
            for (int i = 0; i < b.Instructions.Count; i++)
            {
                if (b.Instructions[i] is Assignment a && a.Left.Count == 1 && !a.Left[0].HasIndex && 
                    a.Right is InitializerList il && il.Exprs.Count == 0)
                {
                    while (i + 1 < b.Instructions.Count)
                    {
                        if (b.Instructions[i + 1] is Assignment a2 && a2.Left.Count == 1 && 
                            a2.Left[0].Identifier == a.Left[0].Identifier && a2.Left[0].HasIndex &&
                            a2.Left[0].TableIndices[0] is Constant { ConstType: Constant.ConstantType.ConstString } c)
                        {
                            il.Exprs.Add(a2.Right);
                            il.Assignments ??= new List<Constant>();
                            il.Assignments.Add(c);
                            if (a2.LocalAssignments != null)
                            {
                                a.LocalAssignments = a2.LocalAssignments;
                            }
                            a2.Left[0].Identifier.UseCount--;
                            b.Instructions.RemoveAt(i + 1);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        }
    }
}