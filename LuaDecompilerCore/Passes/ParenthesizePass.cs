using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Inserts parentheses in all the expressions if they are needed (i.e. the result of an operation is used by an operation
/// with lower precedence: a + b * c + d would become (a + b) * (c + d) for certain expression trees for example
/// </summary>
public class ParenthesizePass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        foreach (var b in f.BlockList)
        {
            foreach (var i in b.Instructions)
            {
                i.Parenthesize();
            }
        }
    }
}