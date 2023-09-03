using System;
using System.Collections.Generic;

namespace LuaDecompilerCore.IR;

/// <summary>
/// Root interface for all nodes in the intermediate representation. Typically this will be either an instruction or
/// "statement", which is an operation in a basic block, or an expression, which is a computation that yields a result.
/// </summary>
public interface IIrNode
{
    /// <summary>
    /// A defined register is a register that is "defined" or has a value written to within this IR node. This adds all
    /// the defined registers to the "defines" set and returns the set.
    /// </summary>
    /// <param name="defines">The set to add defined registers to</param>
    /// <returns>The defines set after the defined registers are added</returns>
    public HashSet<Identifier> GetDefinedRegisters(HashSet<Identifier> defines);

    /// <summary>
    /// A used register is a register that is "used" or has its value read at least once within this IR node. This adds
    /// all the used registers to the "uses" set and returns the set.
    /// </summary>
    /// <param name="uses">The set to add used registers to</param>
    /// <returns>The uses set after the used registers are added</returns>
    public HashSet<Identifier> GetUsedRegisters(HashSet<Identifier> uses);
    
    public bool MatchAny(Func<IIrNode, bool> condition);

    /// <summary>
    /// Calls "function" on all the uses of this node in depth first traversal order.
    /// </summary>
    public void IterateUses(Action<IIrNode, Identifier> function);
}