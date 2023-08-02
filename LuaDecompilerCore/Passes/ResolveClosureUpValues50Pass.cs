using System;
using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Detect the upvalue bindings for the child closures for Lua 5.0
/// </summary>
public class ResolveClosureUpValues50Pass : IPass
{
    public bool RunOnFunction(DecompilationContext decompilationContext, FunctionContext functionContext, Function f)
    {
        var changed = false;
        
        foreach (var b in f.BlockList)
        {
            for (var i = 0; i < b.Instructions.Count; i++)
            {
                // Recognize a closure instruction
                if (b.Instructions[i] is Assignment { Right: Closure c } a)
                {
                    // Fetch the closure bindings from the following instructions
                    for (var j = 0; j < c.Function.UpValueCount; j++)
                    {
                        if (b.Instructions[i + 1] is Assignment 
                            { 
                                IsSingleAssignment: true, 
                                Left.Identifier.RegNum: 0, 
                                Right: IdentifierReference { Identifier.IsRegister: true } ir 
                            } a2)
                        {
                            c.Function.UpValueBindings.Add(ir.Identifier);
                            f.ClosureBoundRegisters.Add(ir.Identifier.RegNum);
                            a.Absorb(a2);
                            b.Instructions.RemoveAt(i + 1);
                            changed = true;
                        }
                        else
                        {
                            throw new Exception("Unrecognized upvalue binding pattern following closure");
                        }
                    }
                }
            }
        }

        return changed;
    }
}