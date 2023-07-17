using System.Text;

namespace LuaCompiler;

/// <summary>
/// Interface for a Lua compiler
/// </summary>
public interface ICompiler
{
    public byte[] CompileSource(string source, Encoding encoding);
}