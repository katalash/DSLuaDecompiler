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

        /// <summary>
        /// If debug info exist, these are the local variables that are assigned if any (null if none are assigned and thus a "temp")
        /// </summary>
        public List<LuaFile.Local> LocalAssignments;
        
        /// <summary>
        /// When this is set to true, the value defined by this is always expression/constant propogated, even if it's used more than once
        /// </summary>
        public bool PropogateAlways = false;

        /// <summary>
        /// This assignment represents an assignment to an indeterminant number of varargs
        /// </summary>
        public bool IsIndeterminantVararg = false;
        public uint VarargAssignmentReg = 0;

        public uint NilAssignmentReg = 0;

        /// <summary>
        /// Is the first assignment of a local variable, and thus starts with "local"
        /// </summary>
        public bool IsLocalDeclaration = false;

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
            if (IsLocalDeclaration)
            {
                ret = "local ";
            }
            if (Left.Count() == 1 && !Left[0].HasIndex && Left[0].Identifier.IType == Identifier.IdentifierType.Global && Right is Closure c)
            {
                return c.Function.PrettyPrint(Left[0].Identifier.Name);
            }
            if (Left.Count() > 0)
            {
                if (Left.Count() == 1 && Left[0].HasIndex && Right is Closure)
                {
                    Left[0].DotNotation = true;
                }
                else
                {
                    for (int i = 0; i < Left.Count(); i++)
                    {
                        ret += Left[i].ToString();
                        if (i != Left.Count() - 1)
                        {
                            ret += ", ";
                        }
                    }
                    ret += " = " + Right;
                }
            }
            else
            {
                ret = Right.ToString();
            }
            return ret;
        }
    }
}
