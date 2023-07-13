using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Interface for a pass that operates on the IR for a function
/// </summary>
public interface IPass
{
    public void RunOnFunction(DecompilationContext context, Function f);
}