using System;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Detects list initializers as a series of statements that serially add data to a newly initialized list
/// </summary>
public class DetectListInitializersPass : IPass
{
    public bool RunOnFunction(DecompilationContext decompilationContext, FunctionContext functionContext, Function f)
    {
        bool changed = false;
        foreach (var b in f.BlockList)
        {
            for (var i = 0; i < b.Instructions.Count; i++)
            {
                if (b.Instructions[i] is Assignment 
                    {
                        IsSingleAssignment: true, 
                        Left: IdentifierReference tableIr,
                        Right: InitializerList
                        {
                            ExpressionsEmpty: true
                        } il 
                    } a)
                {
                    // Eat up any statements that follow that match the initializer list pattern
                    //var initIndex = 1;
                    while (i + 1 < b.Instructions.Count)
                    {
                        if (b.Instructions[i + 1] is Assignment
                            {
                                IsSingleAssignment: true,
                                Left: TableAccess
                                {
                                    Table: IdentifierReference ir,
                                    TableIndex: Constant c
                                }
                            } a2 && 
                            //Math.Abs(c.Number - initIndex) < 0.0001 && 
                            ir.Identifier == tableIr.Identifier)
                        {
                            if (a2.Right == null)
                                throw new Exception("Expected assignment");
                            il.AddTableElement(c, a2.Right);
                            if (a2.LocalAssignments != null)
                            {
                                a.LocalAssignments = a2.LocalAssignments;
                            }
                            a.Absorb(a2);
                            b.Instructions.RemoveAt(i + 1);
                            //initIndex++;
                            changed = true;
                        }
                        else if (b.Instructions[i + 1] is ListRangeAssignment a3 && 
                                 a3.Table.Identifier == tableIr.Identifier)
                        {
                            il.AddListRange(a3.Indices, a3.Values);
                            a.Absorb(a3);
                            b.Instructions.RemoveAt(i + 1);
                            changed = true;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        }

        return changed;
    }
}