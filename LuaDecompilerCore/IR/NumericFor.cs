using System.Linq;
using LuaDecompilerCore.CFG;

namespace LuaDecompilerCore.IR
{
    /// <summary>
    /// A numeric for statement
    /// for var=exp1, exp2, exp3 do
    ///     something
    /// end
    /// </summary>
    public sealed class NumericFor : Instruction
    {
        public readonly Assignment? Initial;
        public readonly Expression? Limit;
        public readonly Expression Increment;

        public readonly BasicBlock Body;
        public readonly BasicBlock? Follow;

        public NumericFor(
            Assignment? initial,
            Expression? limit, 
            Expression increment, 
            BasicBlock body, 
            BasicBlock? follow)
        {
            Initial = initial;
            Limit = limit;
            Increment = increment;
            Body = body;
            Follow = follow;
        }
    }
}
