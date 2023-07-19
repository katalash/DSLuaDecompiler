using System.Linq;

namespace LuaDecompilerCore.IR
{
    /// <summary>
    /// Higher level AST node for encoding if statements
    /// </summary>
    public sealed class IfStatement : Instruction
    {
        public required Expression Condition;
        public CFG.BasicBlock? True = null;
        public CFG.BasicBlock? False = null;
        public CFG.BasicBlock? Follow = null;
        public bool IsElseIf = false;
    }
}
