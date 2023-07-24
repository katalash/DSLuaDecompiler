using System;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Detects list initializers as a series of statements that serially add data to a newly initialized list
/// </summary>
public class DetectListInitializersPass : IPass
{
    public void RunOnFunction(DecompilationContext decompilationContext, FunctionContext functionContext, Function f)
    {
        foreach (var b in f.BlockList)
        {
            for (var i = 0; i < b.Instructions.Count; i++)
            {
                if (b.Instructions[i] is Assignment 
                    {
                        IsSingleAssignment: true, 
                        Left.HasIndex: false, 
                        Right: InitializerList
                        {
                            ExpressionsEmpty: true
                        } il 
                    } a)
                {
                    // Eat up any statements that follow that match the initializer list pattern
                    var initIndex = 1;
                    while (i + 1 < b.Instructions.Count)
                    {
                        if (b.Instructions[i + 1] is Assignment
                            {
                                IsSingleAssignment: true, Left: { HasIndex: true, TableIndex: Constant c }
                            } a2 && 
                            Math.Abs(c.Number - initIndex) < 0.0001 && 
                            a2.Left.Identifier == a.Left.Identifier)
                        {
                            if (a2.Right == null)
                                throw new Exception("Expected assignment");
                            il.Expressions.Add(a2.Right);
                            if (a2.LocalAssignments != null)
                            {
                                a.LocalAssignments = a2.LocalAssignments;
                            }
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