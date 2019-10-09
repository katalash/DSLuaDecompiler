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
            Upvalue
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
        public string Name;
        public Identifier OriginalIdentifier = null;

        // Some stuff to help with analysis
        public uint Regnum = 0;
        public IInstruction DefiningInstruction = null;
        public int UseCount = 0;
        public int PhiUseCount = 0;

        public override string ToString()
        {
            return Name;
        }
    }
}
