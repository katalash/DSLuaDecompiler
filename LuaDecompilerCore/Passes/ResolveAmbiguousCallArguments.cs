using System;
using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Resolves function calls that have 0 as its "b", which means the actual arguments are determined by a
/// previous function with an indefinite return count being the last argument
/// </summary>
public class ResolveAmbiguousCallArguments : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        foreach (var b in f.BlockList)
        {
            Identifier lastAmbiguousReturn = null;
            foreach (var i in b.Instructions)
            {
                switch (i)
                {
                    case Assignment { Right: FunctionCall { HasAmbiguousArgumentCount: true } fc2 } when lastAmbiguousReturn == null:
                        throw new Exception("Error: Ambiguous argument function call without preceding ambiguous return function call");
                    case Assignment { Right: FunctionCall { HasAmbiguousArgumentCount: true } fc2 }:
                    {
                        for (uint r = fc2.BeginArg; r <= lastAmbiguousReturn.RegNum; r++)
                        {
                            fc2.Args.Add(new IdentifierReference(f.GetRegister(r)));
                        }
                        lastAmbiguousReturn = null;
                        break;
                    }
                    case Return { IsIndeterminantReturnCount: true } ret when lastAmbiguousReturn == null:
                        throw new Exception("Error: Ambiguous return without preceding ambiguous return function call");
                    case Return { IsIndeterminantReturnCount: true } ret:
                    {
                        for (var r = ret.BeginRet; r <= lastAmbiguousReturn.RegNum; r++)
                        {
                            ret.ReturnExpressions.Add(new IdentifierReference(f.GetRegister(r)));
                        }

                        break;
                    }
                }

                if (i is Assignment a && a.Left.Count == 1 && !a.Left[0].HasIndex && a.Right is FunctionCall
                    {
                        HasAmbiguousReturnCount: true
                    })
                {
                    lastAmbiguousReturn = a.Left[0].Identifier;
                }
            }
        }
    }
}