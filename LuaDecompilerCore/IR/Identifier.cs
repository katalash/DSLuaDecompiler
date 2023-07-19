namespace LuaDecompilerCore.IR
{
    /// <summary>
    /// An Identifier tracked by the symbol table. Should be unique per scope/closure
    /// </summary>
    public class Identifier
    {
        public enum IdentifierType
        {
            Register,
            Global,
            GlobalTable,
            UpValue,
            Varargs,
        }

        public IdentifierType Type;
        public bool StackUpValue = false; // For lua 5.3
        public required string Name;
        public bool Renamed = false;
        public Identifier? OriginalIdentifier = null;

        // Some stuff to help with analysis
        public uint RegNum = 0;
        public Instruction? DefiningInstruction = null;
        public int UseCount = 0;
        public int PhiUseCount = 0;

        // Used to help reorder some expressions
        public int ConstantId = 0;

        public bool UpValueResolved = false;

        // If this identifier is used by a closure and therefore can't be inlined
        public bool IsClosureBound = false;

        public bool IsRegister => Type == IdentifierType.Register;
        public bool IsGlobal => Type == IdentifierType.Global;
        public bool IsGlobalTable => Type == IdentifierType.GlobalTable;
        public bool IsUpValue => Type == IdentifierType.UpValue;
        public bool IsVarArgs => Type == IdentifierType.Varargs;
    }
}
