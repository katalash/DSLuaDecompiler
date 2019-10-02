using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace luadec.IR
{
    /// <summary>
    /// IL for an assignment operation
    /// Identifier := Expr
    /// </summary>
    public class Assignment : IInstruction
    {
        /// <summary>
        /// Functions can have multiple returns
        /// </summary>
        public List<IdentifierReference> Left;
        public Expression Right;

        public Assignment(Identifier l, Expression r)
        {
            Left = new List<IdentifierReference>();
            Left.Add(new IdentifierReference(l));
            Right = r;
        }

        public Assignment(IdentifierReference l, Expression r)
        {
            Left = new List<IdentifierReference>();
            Left.Add(l);
            Right = r;
        }

        public Assignment(List<IdentifierReference> l, Expression r)
        {
            Left = l;
            Right = r;
        }

        public override HashSet<Identifier> GetDefines(bool regonly)
        {
            var defines = new HashSet<Identifier>();
            foreach (var id in Left)
            {
                // If the reference is not an indirect one (i.e. not an array access), then it is a definition
                if (!id.HasIndex && (!regonly || id.Identifier.IType == Identifier.IdentifierType.Register))
                {
                    defines.Add(id.Identifier);
                }
            }
            return defines;
        }

        public override HashSet<Identifier> GetUses(bool regonly)
        {
            var uses = new HashSet<Identifier>();
            foreach (var id in Left)
            {
                // If the reference is an indirect one (i.e. an array access), then it is a use
                if (id.HasIndex && (!regonly || id.Identifier.IType == Identifier.IdentifierType.Register))
                {
                    uses.UnionWith(id.GetUses(regonly));
                }
            }
            uses.UnionWith(Right.GetUses(regonly));
            return uses;
        }

        public override void RenameDefines(Identifier orig, Identifier newi)
        {
            foreach (var id in Left)
            {
                // If the reference is not an indirect one (i.e. not an array access), then it is a definition
                if (!id.HasIndex && id.Identifier == orig)
                {
                    id.Identifier = newi;
                    id.Identifier.DefiningInstruction = this;
                }
            }
        }

        public override void RenameUses(Identifier orig, Identifier newi)
        {
            foreach (var id in Left)
            {
                // If the reference is an indirect one (i.e. an array access), then it is a use
                if (id.HasIndex)
                {
                    id.RenameUses(orig, newi);
                }
            }
            Right.RenameUses(orig, newi);
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            bool replaced = false;
            foreach (var l in Left)
            {
                replaced = replaced || l.ReplaceUses(orig, sub);
            }
            if (Expression.ShouldReplace(orig, Right))
            {
                replaced = true;
                Right = sub;
            }
            else
            {
                replaced = replaced || Right.ReplaceUses(orig, sub);
            }
            return replaced;
        }

        public override string ToString()
        {
            var ret = "";
            if (Left.Count() > 0)
            {
                if (Left.Count() == 1 && Left[0].HasIndex && Right is Closure)
                {
                    Left[0].DotNotation = true;
                }
                ret = Left[0] + " = " + Right;
            }
            else
            {
                ret = Right.ToString();
            }
            return ret;
        }
    }
}
