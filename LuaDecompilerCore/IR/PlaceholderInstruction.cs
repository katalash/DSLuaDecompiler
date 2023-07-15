namespace LuaDecompilerCore.IR
{
    /// <summary>
    /// Simple string based placeholder until IR is implemented
    /// </summary>
    public class PlaceholderInstruction : Instruction
    {
        public readonly string Placeholder;
        
        public PlaceholderInstruction(string place)
        {
            Placeholder = place;
        }

        public override string ToString()
        {
            return Placeholder;
        }

        public override void Accept(IIrVisitor visitor)
        {
            visitor.VisitPlaceholderInstruction(this);
        }
    }
}
