using System;
using System.Collections.Generic;
using System.Linq;
using LuaDecompilerCore.Utilities;

namespace LuaDecompilerCore.IR
{
    /// <summary>
    /// A single instruction or statement, initially translated from a Lua opcode,
    /// but can be simplified into more powerful "instructions"
    /// </summary>
    public abstract class Instruction : IMatchable
    {
        /// <summary>
        /// The original lua bytecode op within the function that generated this instruction
        /// </summary>
        public int OpLocation = -1;

        /// <summary>
        /// Range of instruction indices in the original IR that this instruction represents
        /// </summary>
        public Interval InstructionIndices;

        /// <summary>
        /// Range of registers that were defined (assigned) in the original IR that this instruction
        /// represents
        /// </summary>
        public Interval DefinedRegisters;

        /// <summary>
        /// Backpointer to the containing block. Used for some analysis
        /// </summary>
        public CFG.BasicBlock? Block = null;

        public bool HasClosure => MatchAny(e => e is Closure);
        
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
        /// "Absorbs" another instruction by updating bookkeeping ranges associated with the other instruction.
        /// Use this when you replace another instruction and make this one the superset (such as when inlining).
        /// </summary>
        /// <param name="instruction"></param>
        public void Absorb(Instruction instruction)
        {
            InstructionIndices = InstructionIndices.UnionWith(instruction.InstructionIndices);
            DefinedRegisters = DefinedRegisters.UnionWith(instruction.DefinedRegisters);
        }

        /// <summary>
        /// Gets all the identifiers that are defined by this instruction and adds them to the input set
        /// </summary>
        public virtual HashSet<Identifier> GetDefines(HashSet<Identifier> defines, bool registersOnly)
        {
            return defines;
        }
        
        /// <summary>
        /// Gets all the identifiers that are used (but not defined) by this instruction and adds them
        /// to the input set
        /// </summary>
        public virtual HashSet<Identifier> GetUses(HashSet<Identifier> uses, bool registersOnly)
        {
            return uses;
        }

        /// <summary>
        /// If this instruction defines only a single identifier, return that identifier
        /// </summary>
        /// <returns></returns>
        public virtual Identifier? GetSingleDefine(bool registersOnly)
        {
            return null;
        }

        public virtual List<Expression> GetExpressions()
        {
            return new List<Expression>();
        }

        public virtual void RenameDefines(Identifier original, Identifier newIdentifier) { }

        public virtual void RenameUses(Identifier original, Identifier newIdentifier) { }

        public virtual bool ReplaceUses(Identifier orig, Expression sub) { return false; }

        public virtual int UseCount(Identifier use) { return 0; }
        
        public override string ToString()
        {
            return FunctionPrinter.DebugPrintInstruction(this);
        }

        public virtual bool MatchAny(Func<IMatchable, bool> condition)
        {
            return condition.Invoke(this);
        }
    }
}
