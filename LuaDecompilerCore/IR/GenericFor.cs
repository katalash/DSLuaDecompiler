using System.Linq;

namespace LuaDecompilerCore.IR
{
    /// <summary>
    /// A numeric for statement
    /// for var in exp do
    ///     something
    /// end
    /// </summary>
    public class GenericFor : Instruction
    {
        public Assignment Iterator;

        public CFG.BasicBlock Body;
        public CFG.BasicBlock Follow;

        public override void Accept(IIrVisitor visitor)
        {
            visitor.VisitGenericFor(this);
        }
    }
}
