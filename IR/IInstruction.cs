using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace luadec.IR
{
    /// <summary>
    /// A single instruction or statement, initially translated from a Lua opcode, but can be simplified into more powerful "instructions"
    /// </summary>
    public abstract class IInstruction
    {
        /// <summary>
        /// The original lua bytecode op within the function that generated this instruction
        /// </summary>
        public int OpLocation = 0;

        /// <summary>
        /// Backpointer to the containing block. Used for some analysis
        /// </summary>
        public CFG.BasicBlock Block = null;

        public virtual void Parenthesize() { }

        /// <summary>
        /// Gets all the identifiers that are defined by this instruction
        /// </summary>
        /// <returns></returns>
        public virtual HashSet<Identifier> GetDefines(bool regonly)
        {
            return new HashSet<Identifier>();
        }

        /// <summary>
        /// Gets all the identifiers that are used (but not defined) by this instruction
        /// </summary>
        /// <returns></returns>
        public virtual HashSet<Identifier> GetUses(bool regonly)
        {
            return new HashSet<Identifier>();
        }

        public virtual List<Expression> GetExpressions()
        {
            return new List<Expression>();
        }

        public virtual void RenameDefines(Identifier orig, Identifier newi) { }

        public virtual void RenameUses(Identifier orig, Identifier newi) { }

        public virtual bool ReplaceUses(Identifier orig, Expression sub) { return false; }

        public virtual string WriteLua(int indentLevel)
        {
            return ToString();
        }
    }
}
