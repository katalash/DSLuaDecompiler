using System.Linq;

namespace LuaDecompilerCore.IR
{
    public class While : Instruction
    {
        public Expression Condition;

        public CFG.BasicBlock Body;
        public CFG.BasicBlock Follow;

        public bool IsPostTested = false;
        public bool IsBlockInlined = false;

        public override void Accept(IIrVisitor visitor)
        {
            visitor.VisitWhile(this);
        }
    }
}
