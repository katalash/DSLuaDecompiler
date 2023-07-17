namespace LuaCompiler;

public class CompileException : Exception
{
    public CompileException(string error) : base(error)
    {
    }
}