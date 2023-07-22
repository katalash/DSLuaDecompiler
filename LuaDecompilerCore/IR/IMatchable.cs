using System;

namespace LuaDecompilerCore.IR;

/// <summary>
/// A node that can be matched using a condition to pattern match the node. All the child matchables will also
/// be tested for the pattern.
/// </summary>
public interface IMatchable
{
    public bool MatchAny(Func<IMatchable, bool> condition);
}