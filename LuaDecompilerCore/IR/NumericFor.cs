using System.Linq;

namespace LuaDecompilerCore.IR
{
    /// <summary>
    /// A numeric for statement
    /// for var=exp1, exp2, exp3 do
    ///     something
    /// end
    /// </summary>
    public class NumericFor : Instruction
    {
        public Assignment Initial;
        public Expression Limit;
        public Expression Increment;

        public CFG.BasicBlock Body;
        public CFG.BasicBlock Follow;

        public override void Accept(IIrVisitor visitor)
        {
            visitor.VisitNumericFor(this);
        }
    }
}
