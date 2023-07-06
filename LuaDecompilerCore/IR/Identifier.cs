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
            Upvalue,
            Varargs,
        }

        public enum ValueType
        {
            Unknown,
            Number,
            Boolean,
            String,
            Table,
            Closure,
        }

        public IdentifierType IType;
        public ValueType VType;
        public bool StackUpvalue = false; // For lua 5.3
        public string Name;
        public bool Renamed = false;
        public Identifier OriginalIdentifier = null;

        // A name fragment that may be set if the identifier is used in certain common patterns. This is not globally or even locally unique; it just
        // serves as input to the renaming function which will make it unique.
        public string HeuristicName;

        // Some stuff to help with analysis
        public uint Regnum = 0;
        public IInstruction DefiningInstruction = null;
        public int UseCount = 0;
        public int PhiUseCount = 0;

        // Used to help reorder some expressions
        public int ConstantID = 0;

        public bool UpvalueResolved = false;

        // If this identifier is used by a closure and therefore can't be inlined
        public bool IsClosureBound = false;

        public override string ToString()
        {
            if (IType == IdentifierType.Varargs)
            {
                return "...";
            }
            return Name;
        }
    }
}
