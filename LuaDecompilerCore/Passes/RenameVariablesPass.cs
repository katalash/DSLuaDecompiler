using System.Collections.Generic;
using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Rename variables from their temporary register based names to something more generic
/// </summary>
public class RenameVariablesPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        var renamed = new HashSet<Identifier>();
        
        // Rename function arguments
        for (var i = 0; i < f.Parameters.Count; i++)
        {
            renamed.Add(f.Parameters[i]);
            if (f.ArgumentNames != null && f.ArgumentNames.Count > i && f.ArgumentNames[i].Name is { } n)
            {
                f.Parameters[i].Name = n;
            }
            else
            {
                f.Parameters[i].Name = $@"arg{i}";
            }
        }
        
        // Rename all the locals
        var localCounter = 0;
        foreach (var b in f.BlockList)
        {
            foreach (var i in b.Instructions)
            {
                if (i is Assignment a)
                {
                    var ll = 0;
                    foreach (var l in a.LeftList)
                    {
                        if (l is { HasIndex: false } && l.Identifier.Type == Identifier.IdentifierType.Register && 
                            !renamed.Contains(l.Identifier) && !l.Identifier.Renamed)
                        {
                            renamed.Add(l.Identifier);
                            if (a.LocalAssignments != null && ll < a.LocalAssignments.Count &&
                                a.LocalAssignments[ll].Name is { } n)
                            {
                                l.Identifier.Name = n;
                            }
                            else
                            {
                                l.Identifier.Name = $@"f{f.FunctionId}_local{localCounter}";
                                localCounter++;
                            }
                            // Needed so upval uses by closures don't rename this
                            l.Identifier.Renamed = true;
                        }
                        ll++;
                    }
                }
            }
        }
    }
}