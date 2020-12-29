using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace luadec.IR
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
