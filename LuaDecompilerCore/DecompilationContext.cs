using LuaDecompilerCore.IR;

namespace LuaDecompilerCore;

/// <summary>
/// Global context for a decompilation that can be used by passes.
/// </summary>
public class DecompilationContext
{
    public GlobalSymbolTable GlobalSymbolTable { get; private set; }
    
    public DecompilationContext(GlobalSymbolTable globalSymbolTable)
    {
        GlobalSymbolTable = globalSymbolTable;
    }
}