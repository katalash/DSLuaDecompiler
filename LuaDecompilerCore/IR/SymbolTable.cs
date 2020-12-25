using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace luadec.IR
{
    /// <summary>
    /// Table to keep track of symbols inside different scopes
    /// </summary>
    public class SymbolTable
    {
        private Stack<Dictionary<String, Identifier>> ScopeStack;
        private List<Dictionary<String, Identifier>> FinalizedScopes;

        public SymbolTable()
        {
            ScopeStack = new Stack<Dictionary<string, Identifier>>();
            FinalizedScopes = new List<Dictionary<string, Identifier>>();
            var globals = new Dictionary<string, Identifier>();
            ScopeStack.Push(globals);
        }

        /// <summary>
        /// Enter a new closure or scope
        /// </summary>
        public void BeginScope()
        {
            var closure = new Dictionary<string, Identifier>();
            ScopeStack.Push(closure);
        }

        public void EndScope()
        {
            FinalizedScopes.Add(ScopeStack.Pop());
        }

        public Identifier GetRegister(uint reg)
        {
            if (!ScopeStack.Peek().ContainsKey($@"REG{reg}"))
            {
                Identifier regi = new Identifier();
                regi.IType = Identifier.IdentifierType.Register;
                regi.VType = Identifier.ValueType.Unknown;
                regi.Name = $@"REG{reg}";
                regi.Regnum = reg;
                ScopeStack.Peek().Add(regi.Name, regi);
            }
            return ScopeStack.Peek()[$@"REG{reg}"];
        }

        public Identifier GetUpvalue(uint upvalue)
        {
            if (!ScopeStack.Peek().ContainsKey($@"UPVAL{upvalue}"))
            {
                Identifier regi = new Identifier();
                regi.IType = Identifier.IdentifierType.Upvalue;
                regi.VType = Identifier.ValueType.Unknown;
                regi.Name = $@"UPVAL{upvalue}";
                ScopeStack.Peek().Add(regi.Name, regi);
            }
            return ScopeStack.Peek()[$@"UPVAL{upvalue}"];
        }

        public Identifier GetGlobal(string global)
        {
            if (!ScopeStack.First().ContainsKey(global))
            {
                Identifier regi = new Identifier();
                regi.IType = Identifier.IdentifierType.Global;
                regi.VType = Identifier.ValueType.Unknown;
                regi.Name = global;
                ScopeStack.First().Add(regi.Name, regi);
            }
            return ScopeStack.First()[global];
        }

        public Identifier GetVarargs()
        {
            if (!ScopeStack.First().ContainsKey("$VARARGS"))
            {
                Identifier regi = new Identifier();
                regi.IType = Identifier.IdentifierType.Varargs;
                regi.VType = Identifier.ValueType.Unknown;
                regi.Name = "$VARARGS";
                ScopeStack.First().Add(regi.Name, regi);
            }
            return ScopeStack.First()["$VARARGS"];
        }

        public  HashSet<Identifier> GetAllRegistersInScope()
        {
            var ret = new HashSet<Identifier>();
            foreach (var reg in ScopeStack.Peek())
            {
                if (reg.Value.IType == Identifier.IdentifierType.Register)
                {
                    ret.Add(reg.Value);
                }
            }
            return ret;
        }
    }
}
