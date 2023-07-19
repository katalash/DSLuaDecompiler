using System.Collections.Generic;
using System.Linq;

namespace LuaDecompilerCore.IR
{
    /// <summary>
    /// A single instruction or statement, initially translated from a Lua opcode,
    /// but can be simplified into more powerful "instructions"
    /// </summary>
    public abstract class Instruction
    {
        /// <summary>
        /// The original lua bytecode op within the function that generated this instruction
        /// </summary>
        public int OpLocation = 0;

        /// <summary>
        /// The instruction index in a basic block before propagation is done
        /// </summary>
        public int PrePropagationIndex = 0;

        /// <summary>
        /// Backpointer to the containing block. Used for some analysis
        /// </summary>
        public CFG.BasicBlock? Block = null;

        public bool HasClosure => GetExpressions().Any(e => e is Closure);
        
        /// <summary>
        /// True if this is an assignment instruction that assigns a closure
        /// </summary>
        public bool IsClosureAssignment => this is Assignment { Right: Closure };

        /// <summary>
        /// True if this instruction pattern matches to a function declaration
        /// </summary>
        public bool IsFunctionDeclaration => this is Assignment
        {
            IsSingleAssignment: true,
            Left: { HasIndex: false, Identifier.IsGlobal: true },
            Right: Closure
        };
        
        public virtual void Parenthesize() { }

        /// <summary>
        /// Gets all the identifiers that are defined by this instruction
        /// </summary>
        /// <returns></returns>
        public virtual HashSet<Identifier> GetDefines(bool registersOnly)
        {
            return new HashSet<Identifier>();
        }

        /// <summary>
        /// Gets all the identifiers that are used (but not defined) by this instruction
        /// </summary>
        /// <returns></returns>
        public virtual HashSet<Identifier> GetUses(bool registersOnly)
        {
            return new HashSet<Identifier>();
        }

        public virtual List<Expression> GetExpressions()
        {
            return new List<Expression>();
        }

        public virtual void RenameDefines(Identifier original, Identifier newIdentifier) { }

        public virtual void RenameUses(Identifier original, Identifier newIdentifier) { }

        public virtual bool ReplaceUses(Identifier orig, Expression sub) { return false; }
    }
}
