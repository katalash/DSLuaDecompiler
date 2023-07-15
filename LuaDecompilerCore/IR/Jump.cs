using System.Collections.Generic;

namespace LuaDecompilerCore.IR
{
    public class Jump : Instruction
    {
        public Label Dest;
        // For debug pretty printing only
        public CFG.BasicBlock BlockDest = null;
        public bool Conditional;
        public Expression Condition;

        // Lua 5.1 and HKS has a post-jump assignment that needs to be put at the top of the successor block
        public Assignment PostTakenAssignment = null;

        public Jump(Label dest)
        {
            Dest = dest;
            Conditional = false;
        }

        public Jump(Label dest, Expression cond)
        {
            Dest = dest;
            Conditional = true;
            Condition = cond;
            if (Condition is BinOp op)
            {
                op.NegateCondition();
            }
        }

        public override void Parenthesize()
        {
            if (Conditional)
            {
                Condition.Parenthesize();
            }
        }

        public override HashSet<Identifier> GetUses(bool registersOnly)
        {
            if (Conditional)
            {
                return Condition.GetUses(registersOnly);
            }
            return base.GetUses(registersOnly);
        }

        public override void RenameUses(Identifier orig, Identifier newIdentifier)
        {
            if (Conditional)
            {
                Condition.RenameUses(orig, newIdentifier);
            }
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            if (!Conditional) return false;
            if (Expression.ShouldReplace(orig, Condition))
            {
                Condition = sub;
                return true;
            }
            return Condition.ReplaceUses(orig, sub);
        }

        public override void Accept(IIrVisitor visitor)
        {
            visitor.VisitJump(this);
        }

        public override List<Expression> GetExpressions()
        {
            var ret = new List<Expression>();
            if (Conditional)
            {
                ret = Condition.GetExpressions();
            }
            return ret;
        }
    }
}
