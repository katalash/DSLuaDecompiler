namespace LuaDecompilerCore.IR
{
    public class Break : Instruction
    {
        public override void Accept(IIrVisitor visitor)
        {
            visitor.VisitBreak(this);
        }
    }
}
