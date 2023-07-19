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
            if (_symbols.TryGetValue(global, out var value)) return value;
            var regi = new Identifier
            {
                Type = Identifier.IdentifierType.Global,
                Name = global,
                ConstantId = constantId
            };
            _symbols.Add(regi.Name, regi);
            return _symbols[global];
        }

        public Identifier GetVarargs()
        {
            if (_symbols.TryGetValue("$VARARGS", out var value)) return value;
            var regi = new Identifier
            {
                Type = Identifier.IdentifierType.Varargs,
                Name = "$VARARGS"
            };
            _symbols.Add(regi.Name, regi);
            return _symbols["$VARARGS"];
        }
    }
}
