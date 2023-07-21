using System.Collections.Generic;
using System.Linq.Expressions;
using LuaDecompilerCore.CFG;

namespace LuaDecompilerCore.IR
{
    public abstract class ConditionalJumpBase : Instruction
    {
        public Expression Condition { get; set; }
        
        protected ConditionalJumpBase(Expression condition)
        {
            Condition = condition;
        }
        
        public override void Parenthesize()
        {
            Condition?.Parenthesize();
        }

        public override void GetUses(HashSet<Identifier> uses, bool registersOnly)
        {
            Condition.GetUses(uses, registersOnly);
        }

        public override void RenameUses(Identifier original, Identifier newIdentifier)
        {
            Condition.RenameUses(original, newIdentifier);
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            if (Expression.ShouldReplace(orig, Condition))
            {
                Condition = sub;
                return true;
            }
            return Condition.ReplaceUses(orig, sub);
        }

        public override List<Expression> GetExpressions()
        {
            return Condition.GetExpressions();
        }
    }

    /// <summary>
    /// Convenience interface for all jumps to labels
    /// </summary>
    public interface IJumpLabel
    {
        public Label Destination { get; set; }
    }

    /// <summary>
    /// Convenience interface for all jumps to blocks
    /// </summary>
    public interface IJump
    {
        public BasicBlock Destination { get; set; }
    }

    /// <summary>
    /// Unconditional jump to a label
    /// </summary>
    public sealed class JumpLabel : Instruction, IJumpLabel
    {
        /// <summary>
        /// The destination label for the jump
        /// </summary>
        public Label Destination { get; set; }
        
        public JumpLabel(Label destination)
        {
            Destination = destination;
        }
    }

    /// <summary>
    /// Unconditional jump to a block
    /// </summary>
    public sealed class Jump : Instruction, IJump
    {
        /// <summary>
        /// The destination block for the jump. Typically used for printing.
        /// </summary>
        public BasicBlock Destination { get; set; }

        public Jump(BasicBlock destination)
        {
            Destination = destination;
        }
    }

    /// <summary>
    /// Conditional jump to a label
    /// </summary>
    public sealed class ConditionalJumpLabel : ConditionalJumpBase, IJumpLabel
    {
        /// <summary>
        /// The destination label for the jump
        /// </summary>
        public Label Destination { get; set; }
        
        /// <summary>
        /// Lua 5.1+ and HKS may have a post-jump assignment that is executed after the jump is taken.
        /// This assignment needs to be added to the successor block once conversion to control flow graph
        /// is done.
        /// </summary>
        public readonly Assignment? PostTakenAssignment;

        public ConditionalJumpLabel(Label destination, Expression condition, Assignment? postTakenAssignment = null) : 
            base(condition)
        {
            Destination = destination;
            PostTakenAssignment = postTakenAssignment;
            if (Condition is BinOp op)
            {
                op.NegateCondition();
            }
        }
    }

    /// <summary>
    /// Conditional jump to a block
    /// </summary>
    public sealed class ConditionalJump : ConditionalJumpBase, IJump
    {
        /// <summary>
        /// The destination block for the jump. Typically used for printing.
        /// </summary>
        public BasicBlock Destination { get; set; }
        
        public ConditionalJump(BasicBlock destination, Expression condition) : 
            base(condition)
        {
            Destination = destination;
        }
    }
}
