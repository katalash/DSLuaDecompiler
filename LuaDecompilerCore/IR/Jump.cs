using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace luadec.IR
{
    public class Jump : IInstruction
    {
        public Label Dest;
        // For debug pretty printing only
        public CFG.BasicBlock BBDest = null;
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

        public override HashSet<Identifier> GetUses(bool regonly)
        {
            if (Conditional)
            {
                return Condition.GetUses(regonly);
            }
            return base.GetUses(regonly);
        }

        public override void RenameUses(Identifier orig, Identifier newi)
        {
            if (Conditional)
            {
                Condition.RenameUses(orig, newi);
            }
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            if (Conditional)
            {
                if (Expression.ShouldReplace(orig, Condition))
                {
                    Condition = sub;
                    return true;
                }
                else
                {
                    return Condition.ReplaceUses(orig, sub);
                }
            }
            return false;
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

        public override string ToString()
        {
            string ret = "";
            if (Conditional)
            {
                ret += $@"if {Condition} else ";
            }
            if (BBDest != null)
            {
                ret += "goto " + BBDest;
            }
            else
            {
                ret += "goto " + Dest;
            }
            return ret;
        }
    }
}
