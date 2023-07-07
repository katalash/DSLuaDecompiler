#nullable enable
using System.Collections.Generic;

namespace LuaDecompilerCore;

/// <summary>
/// Options to control decompilation. Largely for debugging purposes.
/// </summary>
public class DecompilationOptions
{
    /// <summary>
    /// Function Ids that are included in the decompilation. For closures, the parent function must also be included for
    /// the closure to be included in the decompilation. Null means all function Ids are compiled.
    /// </summary>
    public IReadOnlySet<int>? IncludedFunctionIds = null;

    /// <summary>
    /// Function Ids that are explicitly excluded from the decompilation. Excluding a function will exclude any closures
    /// that are contained in the function.
    /// </summary>
    public IReadOnlySet<int> ExcludedFunctionIds = new HashSet<int>();

    /// <summary>
    /// Debug option to dump intermediate representation to the output after each named pass. Passing "all" as a pass
    /// name will dump the IR for all the passes.
    /// </summary>
    public IReadOnlySet<string> DumpIrPasses = new HashSet<string>();
    
    /// <summary>
    /// Add comments to the decompiled output with various debugging info.
    /// </summary>
    public bool OutputDebugComments = false;
}