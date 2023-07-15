namespace LuaDecompilerCore.IR
{
    public class Continue : Instruction
    {
        public override void Accept(IIrVisitor visitor)
        {
            visitor.VisitContinue(this);
        }
    }
}
