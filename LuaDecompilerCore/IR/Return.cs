using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace luadec.IR
{
    class Return : IInstruction
    {
        public List<Expression> ReturnExpressions;
        public uint BeginRet = 0;
        public bool IsIndeterminantReturnCount = false;

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

        public override HashSet<Identifier> GetUses(bool regonly)
        {
            var uses = new HashSet<Identifier>();
            foreach (var exp in ReturnExpressions)
            {
                uses.UnionWith(exp.GetUses(regonly));
            }
            return uses;
        }

        public override void RenameUses(Identifier orig, Identifier newi)
        {
            foreach (var exp in ReturnExpressions)
            {
                exp.RenameUses(orig, newi);
            }
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            bool replace = false;
            for (int i = 0; i < ReturnExpressions.Count(); i++)
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

        public override List<Expression> GetExpressions()
        {
            var ret = new List<Expression>();
            foreach (var r in ReturnExpressions)
            {
                ret.AddRange(r.GetExpressions());
            }
            return ret;
        }

        public override string ToString()
        {
            string ret = "return ";
            for (int i = 0; i < ReturnExpressions.Count(); i++)
            {
                ret += ReturnExpressions[i].ToString();
                if (i != ReturnExpressions.Count() - 1)
                {
                    ret += ", ";
                }
            }
            return ret;
        }
    }
}
