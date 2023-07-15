using System.Collections.Generic;
using System.Linq;

namespace LuaDecompilerCore.IR
{
    public class Return : Instruction
    {
        public List<Expression> ReturnExpressions;
        public uint BeginRet = 0;
        public bool IsIndeterminantReturnCount = false;
        public bool IsImplicit = false;
        public bool IsTailReturn = false;

        public Return(List<Expression> expr)
        {
            ReturnExpressions = expr;
        }

        public Return(Expression expr)
        {
            ReturnExpressions = new List<Expression>();
            ReturnExpressions.Add(expr);
        }

        public override void Parenthesize()
        {
            ReturnExpressions.ForEach(x => x.Parenthesize());
        }

        public override HashSet<Identifier> GetUses(bool registersOnly)
        {
            var uses = new HashSet<Identifier>();
            foreach (var exp in ReturnExpressions)
            {
                uses.UnionWith(exp.GetUses(registersOnly));
            }
            return uses;
        }

        public override void RenameUses(Identifier orig, Identifier newIdentifier)
        {
            foreach (var exp in ReturnExpressions)
            {
                exp.RenameUses(orig, newIdentifier);
            }
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            bool replace = false;
            for (int i = 0; i < ReturnExpressions.Count; i++)
            {
                if (Expression.ShouldReplace(orig, ReturnExpressions[i]))
                {
                    ReturnExpressions[i] = sub;
                    replace = true;
                }
                else
                {
                    replace = replace || ReturnExpressions[i].ReplaceUses(orig, sub);
                }
            }
            return replace;
        }

        public override void Accept(IIrVisitor visitor)
        {
            visitor.VisitReturn(this);
        }

        public override List<Expression> GetExpressions()
        {
            var ret = new List<Expression>();
            foreach (var r in ReturnExpressions)
            {
                ret.AddRange(r.GetExpressions());
            }
            return ret;
        }
    }
}
