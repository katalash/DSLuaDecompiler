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
            for (var i = 0; i < b.Instructions.Count; i++)
            {
                // Recognize a closure instruction
                if (b.Instructions[i] is Assignment { Right: Closure c })
                {
                    // Fetch the closure bindings from the following instructions
                    for (var j = 0; j < c.Function.UpValueCount; j++)
                    {
                        if (b.Instructions[i + 1] is Assignment 
                            { 
                                IsSingleAssignment: true, 
                                Left.Identifier.RegNum: 0, 
                                Right: IdentifierReference { Identifier.IsRegister: true } ir 
                            })
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
                    
                    // Update upValue get/set instructions to new parent identifiers
                    foreach (var get in c.Function.GetUpValueInstructions)
                    {
                        if (get.Right is IdentifierReference right) 
                            right.Identifier = c.Function.UpValueBindings[(int)right.Identifier.RegNum];
                    }
                    
                    foreach (var get in c.Function.SetUpValueInstructions)
                    {
                        get.Left.Identifier = c.Function.UpValueBindings[(int)get.Left.Identifier.RegNum];
                    }
                }
            }
        }
    }
}