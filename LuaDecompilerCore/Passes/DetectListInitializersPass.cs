using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Detects list initializers as a series of statements that serially add data to a newly initialized list
/// </summary>
public class DetectListInitializersPass : IPass
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
                    // Eat up any statements that follow that match the initializer list pattern
                    int initIndex = 1;
                    while (i + 1 < b.Instructions.Count)
                    {
                        if (b.Instructions[i + 1] is Assignment a2 && a2.Left.Count == 1 && 
                            a2.Left[0].Identifier == a.Left[0].Identifier && a2.Left[0].HasIndex &&
                            a2.Left[0].TableIndices[0] is Constant c && c.Number == initIndex)
                        {
                            il.Exprs.Add(a2.Right);
                            if (a2.LocalAssignments != null)
                            {
                                a.LocalAssignments = a2.LocalAssignments;
                            }
                            a2.Left[0].Identifier.UseCount--;
                            b.Instructions.RemoveAt(i + 1);
                            initIndex++;
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