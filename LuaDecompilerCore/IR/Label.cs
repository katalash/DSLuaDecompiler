namespace LuaDecompilerCore.IR
{
    /// <summary>
    /// A label that represents a jump target
    /// </summary>
    public sealed class Label : Instruction
    {
        /// <summary>
        /// Used to generate unique label names
        /// </summary>
        private static int _labelCount = 0;

        /// <summary>
        /// How many instructions use this label. Used to delete labels in some optimizations
        /// </summary>
        public int UsageCount = 0;

        public readonly string LabelName;

        public Label()
        {
            LabelName = $@"Label_{_labelCount}";
            _labelCount++;
        }
    }
}
