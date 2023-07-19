using System.Collections.Generic;

namespace LuaDecompilerCore.IR
{
    public sealed class Return : Instruction
    {
        public readonly List<Expression> ReturnExpressions;
        public uint BeginRet = 0;
        public bool IsAmbiguousReturnCount = false;
        public bool IsImplicit = false;
        public bool IsTailReturn = false;

        public Return(List<Expression> expr)
        {
            ReturnExpressions = expr;
        }

        public Return(Expression expr)
        {
            ReturnExpressions = new List<Expression> { expr };
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

        public override void RenameUses(Identifier original, Identifier newIdentifier)
        {
            foreach (var exp in ReturnExpressions)
            {
                exp.RenameUses(original, newIdentifier);
            }
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            var replace = false;
            for (var i = 0; i < ReturnExpressions.Count; i++)
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
    }
}
