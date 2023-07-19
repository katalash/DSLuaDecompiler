using System.Linq;
using LuaDecompilerCore.CFG;

namespace LuaDecompilerCore.IR
{
    /// <summary>
    /// A numeric for statement
    /// for var in exp do
    ///     something
    /// end
    /// </summary>
    public sealed class GenericFor : Instruction
    {
        public readonly Assignment Iterator;
        public readonly BasicBlock Body;
        public readonly BasicBlock? Follow;

        public GenericFor(Assignment iterator, BasicBlock body, BasicBlock? follow)
        {
            Iterator = iterator;
            Body = body;
            Follow = follow;
        }
    }
}
