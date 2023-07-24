using System;
using System.Collections.Generic;
using System.Linq;

namespace LuaDecompilerCore.IR
{
    /// <summary>
    /// Table to keep track of global symbols
    /// </summary>
    public class GlobalSymbolTable
    {
        private readonly Dictionary<string, Identifier> _symbols;

        public GlobalSymbolTable()
        {
            _symbols = new Dictionary<string, Identifier>();
        }

        public Identifier GetGlobal(string global, int constantId)
        {
            return Identifier.GetGlobal((uint)constantId);
        }

        public Identifier GetVarargs()
        {
            return Identifier.GetVarArgs();
        }
    }
}
