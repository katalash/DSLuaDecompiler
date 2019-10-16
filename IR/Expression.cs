using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace luadec.IR
{
    /// <summary>
    /// Base class for an expression, which can basically do anything expressive
    /// </summary>
    public class Expression
    {
        public virtual HashSet<Identifier> GetUses(bool regonly)
        {
            return new HashSet<Identifier>();
        }

        public virtual void RenameUses(Identifier orig, Identifier newi) { }

        public static bool ShouldReplace(Identifier orig, Expression cand)
        {
            return (cand is IdentifierReference ident && !ident.HasIndex && ident.Identifier == orig);
        }

        public virtual bool ReplaceUses(Identifier orig, Expression sub) { return false; }
    }

    public class Constant : Expression
    {
        public enum ConstantType
        {
            ConstNumber,
            ConstString,
            ConstBool,
            ConstTable,
            ConstVarargs,
            ConstNil,
        }

        public ConstantType ConstType;
        public double Number;
        public string String;
        public bool Boolean;

        public Constant(double num)
        {
            ConstType = ConstantType.ConstNumber;
            Number = num;
        }

        public Constant(string str)
        {
            ConstType = ConstantType.ConstString;
            String = str;
        }

        public Constant(bool b)
        {
            ConstType = ConstantType.ConstBool;
            Boolean = b;
        }

        public Constant(ConstantType typ)
        {
            ConstType = typ;
        }

        public override string ToString()
        {
            switch (ConstType)
            {
                case ConstantType.ConstNumber:
                    return Number.ToString();
                case ConstantType.ConstString:
                    return "\"" + String + "\"";
                case ConstantType.ConstBool:
                    return Boolean ? "true" : "false";
                case ConstantType.ConstTable:
                    return "{}";
                case ConstantType.ConstVarargs:
                    return "...";
                case ConstantType.ConstNil:
                    return "nil";
            }
            return "";
        }
    }

    public class Closure : Expression
    {
        public Function Function;

        public Closure(Function fun)
        {
            Function = fun;
        }

        public override string ToString()
        {
            return Function.ToString();
        }
    }

    public class IdentifierReference : Expression
    {
        public Identifier Identifier;
        public bool HasIndex = false;
        public Expression TableIndex = null;
        public bool DotNotation = false;

        public IdentifierReference(Identifier id)
        {
            Identifier = id;
        }

        public IdentifierReference(Identifier id, Expression index)
        {
            Identifier = id;
            HasIndex = true;
            TableIndex = index;
        }

        public override HashSet<Identifier> GetUses(bool regonly)
        {
            var ret = new HashSet<Identifier>();
            if (!regonly || Identifier.IType == Identifier.IdentifierType.Register)
            {
                ret.Add(Identifier);
            }
            if (HasIndex)
            {
                ret.UnionWith(TableIndex.GetUses(regonly));
            }
            return ret;
        }

        public override void RenameUses(Identifier orig, Identifier newi)
        {
            if (Identifier == orig)
            {
                Identifier = newi;
                Identifier.UseCount++;
            }
            if (HasIndex)
            {
                TableIndex.RenameUses(orig, newi);
            }
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            bool changed = false;
            if (HasIndex && ShouldReplace(orig, TableIndex))
            {
                TableIndex = sub;
                changed = true;
            }
            else if (HasIndex)
            {
                changed = TableIndex.ReplaceUses(orig, sub);
            }
            if (orig == Identifier && sub is IdentifierReference ir && !ir.HasIndex)
            {
                Identifier = ir.Identifier;
                changed = true;
            }
            return changed;
        }

        public override string ToString()
        {
            string ret = Identifier.ToString();
            if (HasIndex)
            {
                ret += $@"[{TableIndex.ToString()}]";
                if (DotNotation && TableIndex is Constant c && c.ConstType == Constant.ConstantType.ConstString)
                {
                    ret = Identifier.ToString() + "." + c.String;
                }
            }
            return ret;
        }
    }

    public class Concat : Expression
    {
        public List<Expression> Exprs;

        public Concat(List<Expression> expr)
        {
            Exprs = expr;
        }

        public override HashSet<Identifier> GetUses(bool regonly)
        {
            var ret = new HashSet<Identifier>();
            foreach (var arg in Exprs)
            {
                ret.UnionWith(arg.GetUses(regonly));
            }
            return ret;
        }

        public override void RenameUses(Identifier orig, Identifier newi)
        {
            foreach (var arg in Exprs)
            {
                arg.RenameUses(orig, newi);
            }
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            bool replaced = false;
            for (int i = 0; i < Exprs.Count(); i++)
            {
                if (ShouldReplace(orig, Exprs[i]))
                {
                    Exprs[i] = sub;
                    replaced = true;
                }
                else
                {
                    replaced = replaced || Exprs[i].ReplaceUses(orig, sub);
                }
            }
            return replaced;
        }

        public override string ToString()
        {
            string ret = "";

            // Pattern match special lua this call
            for (int i = 0; i < Exprs.Count(); i++)
            {
                ret += Exprs[i].ToString();
                if (i != Exprs.Count() - 1)
                {
                    ret += " .. ";
                }
            }
            return ret;
        }
    }

    public class InitializerList : Expression
    {
        public List<Expression> Exprs;

        public InitializerList(List<Expression> expr)
        {
            Exprs = expr;
        }

        public override HashSet<Identifier> GetUses(bool regonly)
        {
            var ret = new HashSet<Identifier>();
            foreach (var arg in Exprs)
            {
                ret.UnionWith(arg.GetUses(regonly));
            }
            return ret;
        }

        public override void RenameUses(Identifier orig, Identifier newi)
        {
            foreach (var arg in Exprs)
            {
                arg.RenameUses(orig, newi);
            }
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            bool replaced = false;
            for (int i = 0; i < Exprs.Count(); i++)
            {
                if (ShouldReplace(orig, Exprs[i]))
                {
                    Exprs[i] = sub;
                    replaced = true;
                }
                else
                {
                    replaced = replaced || Exprs[i].ReplaceUses(orig, sub);
                }
            }
            return replaced;
        }

        public override string ToString()
        {
            string ret = "{";

            // Pattern match special lua this call
            for (int i = 0; i < Exprs.Count(); i++)
            {
                ret += Exprs[i].ToString();
                if (i != Exprs.Count() - 1)
                {
                    ret += ", ";
                }
            }
            ret += "}";
            return ret;
        }
    }

    public class BinOp : Expression
    {
        public enum OperationType
        {
            OpAdd,
            OpSub,
            OpMul,
            OpDiv,
            OpMod,
            OpPow,
            OpEqual,
            OpNotEqual,
            OpLessThan,
            OpLessEqual,
            OpGreaterThan,
            OpGreaterEqual,
            OpAnd,
            OpOr,
            OpLoopCompare,
        }

        public Expression Left;
        public Expression Right;
        public OperationType Operation;

        public BinOp(Expression left, Expression right, OperationType op)
        {
            Left = left;
            Right = right;
            Operation = op;
        }

        public BinOp NegateCondition()
        {
            switch (Operation)
            {
                case OperationType.OpEqual:
                    Operation = OperationType.OpNotEqual;
                    break;
                case OperationType.OpNotEqual:
                    Operation = OperationType.OpEqual;
                    break;
                case OperationType.OpLessThan:
                    Operation = OperationType.OpGreaterEqual;
                    break;
                case OperationType.OpLessEqual:
                    Operation = OperationType.OpGreaterThan;
                    break;
                case OperationType.OpGreaterThan:
                    Operation = OperationType.OpLessEqual;
                    break;
                case OperationType.OpGreaterEqual:
                    Operation = OperationType.OpLessThan;
                    break;
                case OperationType.OpLoopCompare:
                    break;
                default:
                    throw new Exception("Attempting to negate non-conditional binary operation");
            }
            return this;
        }

        public bool IsCompare()
        {
            switch (Operation)
            {
                case OperationType.OpEqual:
                case OperationType.OpNotEqual:
                case OperationType.OpLessThan:
                case OperationType.OpLessEqual:
                case OperationType.OpGreaterThan:
                case OperationType.OpGreaterEqual:
                case OperationType.OpLoopCompare:
                    return true;
            }
            return false;
        }

        public override HashSet<Identifier> GetUses(bool regonly)
        {
            var ret = new HashSet<Identifier>();
            ret.UnionWith(Left.GetUses(regonly));
            ret.UnionWith(Right.GetUses(regonly));
            return ret;
        }

        public override void RenameUses(Identifier orig, Identifier newi)
        {
            Left.RenameUses(orig, newi);
            Right.RenameUses(orig, newi);
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            bool replaced = false;
            if (ShouldReplace(orig, Left))
            {
                Left = sub;
                replaced = true;
            }
            else
            {
                replaced = replaced || Left.ReplaceUses(orig, sub);
            }
            if (ShouldReplace(orig, Right))
            {
                Right = sub;
                replaced = true;
            }
            else
            {
                replaced = replaced || Right.ReplaceUses(orig, sub);
            }
            return replaced;
        }

        public override string ToString()
        {
            string op = "";
            switch (Operation)
            {
                case OperationType.OpAdd:
                    op = "+";
                    break;
                case OperationType.OpDiv:
                    op = "/";
                    break;
                case OperationType.OpMod:
                    op = "%";
                    break;
                case OperationType.OpMul:
                    op = "*";
                    break;
                case OperationType.OpPow:
                    op = "^";
                    break;
                case OperationType.OpSub:
                    op = "-";
                    break;
                case OperationType.OpEqual:
                    op = "==";
                    break;
                case OperationType.OpNotEqual:
                    op = "~=";
                    break;
                case OperationType.OpLessThan:
                    op = "<";
                    break;
                case OperationType.OpLessEqual:
                    op = "<=";
                    break;
                case OperationType.OpGreaterThan:
                    op = ">";
                    break;
                case OperationType.OpGreaterEqual:
                    op = ">=";
                    break;
                case OperationType.OpAnd:
                    op = "and";
                    break;
                case OperationType.OpOr:
                    op = "or";
                    break;
                case OperationType.OpLoopCompare:
                    op = ">?=";
                    break;
            }
            return $@"{Left} {op} {Right}";
        }
    }

    public class UnaryOp : Expression
    {
        public enum OperationType
        {
            OpNegate,
            OpNot,
            OpLength,
        }

        public Expression Exp;
        public OperationType Operation;

        public UnaryOp(Expression exp, OperationType op)
        {
            Exp = exp;
            Operation = op;
        }

        public override HashSet<Identifier> GetUses(bool regonly)
        {
            var ret = new HashSet<Identifier>();
            ret.UnionWith(Exp.GetUses(regonly));
            return ret;
        }

        public override void RenameUses(Identifier orig, Identifier newi)
        {
            Exp.RenameUses(orig, newi);
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            if (ShouldReplace(orig, Exp))
            {
                Exp = sub;
                return true;
            }
            else
            {
                return Exp.ReplaceUses(orig, sub);
            }
        }

        public override string ToString()
        {
            string op = "";
            switch (Operation)
            {
                case OperationType.OpNegate:
                    op = "-";
                    break;
                case OperationType.OpNot:
                    op = "not ";
                    break;
                case OperationType.OpLength:
                    op = "#";
                    break;
            }
            return $@"{op}{Exp}";
        }
    }

    public class FunctionCall : Expression
    {
        public Expression Function;
        public List<Expression> Args;

        // Lua memes
        public uint BeginArg = 0;
        public bool IsIndeterminantReturnCount = false;
        public bool IsIndeterminantArgumentCount = false;

        public FunctionCall(Expression fun, List<Expression> args)
        {
            Function = fun;
            Args = args;
        }

        public override HashSet<Identifier> GetUses(bool regonly)
        {
            var ret = new HashSet<Identifier>();
            foreach (var arg in Args)
            {
                ret.UnionWith(arg.GetUses(regonly));
            }
            ret.UnionWith(Function.GetUses(regonly));
            return ret;
        }

        public override void RenameUses(Identifier orig, Identifier newi)
        {
            Function.RenameUses(orig, newi);
            foreach (var arg in Args)
            {
                arg.RenameUses(orig, newi);
            }
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            bool replaced = false;
            if (ShouldReplace(orig, Function) && (sub is IdentifierReference || sub is Constant))
            {
                Function = sub;
                replaced = true;
            }
            else
            {
                replaced = Function.ReplaceUses(orig, sub);
            }
            for (int i = 0; i < Args.Count(); i++)
            {
                if (ShouldReplace(orig, Args[i]))
                {
                    Args[i] = sub;
                    replaced = true;
                }
                else
                {
                    replaced = replaced || Args[i].ReplaceUses(orig, sub);
                }
            }
            return replaced;
        }

        public override string ToString()
        {
            string ret = "";

            // Pattern match special lua this call
            int beginarg = 0;
            if (Function is IdentifierReference ir && ir.HasIndex &&
                ir.TableIndex is Constant c && c.ConstType == Constant.ConstantType.ConstString)
            {
                if (Args.Count() >= 1 && Args[0] is IdentifierReference thisir && !thisir.HasIndex && thisir.Identifier == ir.Identifier)
                {
                    ret += $@"{ir.Identifier.ToString()}:{c.String}(";
                    beginarg = 1;
                }
                else
                {
                    ret += $@"{ir.Identifier.ToString()}.{c.String}(";
                }
            }
            else
            {
                ret += Function.ToString() + "(";
            }
            for (int i = beginarg; i < Args.Count(); i++)
            {
                ret += Args[i].ToString();
                if (i != Args.Count() - 1)
                {
                    ret += ", ";
                }
            }
            ret += ")";
            return ret;
        }
    }
}
