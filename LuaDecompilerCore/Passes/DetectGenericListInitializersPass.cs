using System;
using System.Diagnostics;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Detects list initializers as a series of statements that serially add data to a newly initialized list
/// </summary>
public class DetectGenericListInitializersPass : IPass
{
    public void RunOnFunction(DecompilationContext decompilationContext, FunctionContext functionContext, Function f)
    {
        foreach (var b in f.BlockList)
        {
            for (var i = 0; i < b.Instructions.Count; i++)
            {
                if (b.Instructions[i] is Assignment { 
                        IsSingleAssignment: true, 
                        Left.HasIndex: false, 
                        Right: InitializerList { Expressions.Count: 0 } il 
                    } a)
                {
                    while (i + 1 < b.Instructions.Count)
                    {
                        if (b.Instructions[i + 1] is Assignment
                            {
                                IsSingleAssignment: true, 
                                Left:
                                {
                                    HasIndex: true, 
                                    TableIndex: Constant { ConstType: Constant.ConstantType.ConstString } c
                                }
                            } a2 && 
                            a2.Left.Identifier == a.Left.Identifier)
                        {
                            if (a2.Right == null)
                                throw new Exception("Expected assigned value");
                            il.Expressions.Add(a2.Right);
                            il.Assignments.Add(c);
                            if (a2.LocalAssignments != null)
                            {
                                a.LocalAssignments = a2.LocalAssignments;
                            }
                            a.Absorb(a2);
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