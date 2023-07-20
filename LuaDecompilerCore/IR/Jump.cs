using System.Collections.Generic;

namespace LuaDecompilerCore.IR
{
    public sealed class Jump : Instruction
    {
        public Label Dest;
        // For debug pretty printing only
        public CFG.BasicBlock? BlockDest = null;
        public Expression? Condition;
        public bool Conditional => Condition != null;

        // Lua 5.1 and HKS has a post-jump assignment that needs to be put at the top of the successor block
        public Assignment? PostTakenAssignment = null;

        public Jump(Label dest)
        {
            Dest = dest;
        }

        public Jump(Label dest, Expression cond)
        {
            Dest = dest;
            Condition = cond;
            if (Condition is BinOp op)
            {
                op.NegateCondition();
            }
        }

        public override void Parenthesize()
        {
            Condition?.Parenthesize();
        }

        public override void GetUses(HashSet<Identifier> uses, bool registersOnly)
        {
            if (Condition != null)
                Condition.GetUses(uses, registersOnly);
            else
                base.GetUses(uses, registersOnly);
        }

        public override void RenameUses(Identifier original, Identifier newIdentifier)
        {
            Condition?.RenameUses(original, newIdentifier);
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            if (Condition is null) return false;
            if (Expression.ShouldReplace(orig, Condition))
            {
                Condition = sub;
                return true;
            }
            return Condition.ReplaceUses(orig, sub);
        }

        public override List<Expression> GetExpressions()
        {
            return Condition != null ? Condition.GetExpressions() : new List<Expression>();
        }
    }
}
