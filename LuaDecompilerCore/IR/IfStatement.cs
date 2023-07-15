using System.Linq;

namespace LuaDecompilerCore.IR
{
    /// <summary>
    /// Higher level AST node for encoding if statements
    /// </summary>
    public class IfStatement : Instruction
    {
        public Expression Condition;
        public CFG.BasicBlock True = null;
        public CFG.BasicBlock False = null;
        public CFG.BasicBlock Follow = null;
        public bool IsElseIf = false;

        public override void Accept(IIrVisitor visitor)
        {
            visitor.VisitIfStatement(this);
        }
    }
}
