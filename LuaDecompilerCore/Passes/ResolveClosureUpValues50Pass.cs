using System;
using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Detect the upvalue bindings for the child closures for Lua 5.0
/// </summary>
public class ResolveClosureUpValues50Pass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        foreach (var b in f.BlockList)
        {
            for (int i = 0; i < b.Instructions.Count; i++)
            {
                // Recognize a closure instruction
                if (b.Instructions[i] is Assignment { Right: Closure c })
                {
                    // Fetch the closure bindings from the following instructions
                    for (int j = 0; j < c.Function.UpValCount; j++)
                    {
                        if (b.Instructions[i + 1] is Assignment ca && 
                            ca.Left.Count == 1 && 
                            ca.Left[0].Identifier.RegNum == 0 &&
                            ca.Right is IdentifierReference ir &&
                            ir.Identifier.Type == Identifier.IdentifierType.Register)
                        {
                            c.Function.UpValueBindings.Add(ir.Identifier);
                            ir.Identifier.IsClosureBound = true;
                            b.Instructions.RemoveAt(i + 1);
                        }
                        else
                        {
                            throw new Exception("Unrecognized upvalue binding pattern following closure");
                        }
                    }
                }
            }
        }
    }
}