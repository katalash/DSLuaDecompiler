using System;
using System.Collections.Generic;
using System.Linq;

namespace LuaDecompilerCore.IR
{
    /// <summary>
    /// Base class for an expression, which can basically do anything expressive
    /// </summary>
    public class Expression
    {
        public virtual HashSet<Identifier> GetUses(bool registerOnly)
        {
            return new HashSet<Identifier>();
        }

        public virtual void RenameUses(Identifier orig, Identifier newIdentifier) { }

        public static bool ShouldReplace(Identifier orig, Expression cand)
        {
            return (cand is IdentifierReference ident && ident.TableIndices.Count == 0 && ident.Identifier == orig);
        }

        public virtual bool ReplaceUses(Identifier orig, Expression sub) { return false; }

        public virtual void Parenthesize() { return; }

        public virtual List<Expression> GetExpressions()
        {
            return new List<Expression> { this };
        }

        public virtual int GetLowestConstantId()
        {
            return -1;
        }
    }

    public class Constant : Expression
    {
        public enum ConstantType
        {
            ConstNumber,
            ConstInteger,
            ConstString,
            ConstBool,
            ConstTable,
            ConstVarargs,
            ConstNil,
        }

        public ConstantType ConstType;
        public double Number;
        public ulong Integer;
        public string String;
        public bool Boolean;

        public int ConstantID;

        public Constant(double num, int id)
        {
            ConstType = ConstantType.ConstNumber;
            Number = num;
            ConstantID = id;
        }

        public Constant(ulong inum, int id)
        {
            ConstType = ConstantType.ConstInteger;
            Integer = inum;
            ConstantID = id;
        }

        public Constant(string str, int id)
        {
            ConstType = ConstantType.ConstString;
            String = str;
            ConstantID = id;
        }

        public Constant(bool b, int id)
        {
            ConstType = ConstantType.ConstBool;
            Boolean = b;
            ConstantID = id;
        }

        public Constant(ConstantType typ, int id)
        {
            ConstType = typ;
            ConstantID = id;
        }

        public override string ToString()
        {
            switch (ConstType)
            {
                case ConstantType.ConstNumber:
                    return Number.ToString();
                case ConstantType.ConstInteger:
                    return Integer.ToString();
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

        public override int GetLowestConstantId()
        {
            return ConstantID;
        }
    }

    public class Closure : Expression
    {
        public Function Function;

        public Closure(Function fun)
        {
            Function = fun;
        }

        public override HashSet<Identifier> GetUses(bool registerOnly)
        {
            return Function.UpValueBindings.Where(
                e => !registerOnly || e.Type == Identifier.IdentifierType.Register).ToHashSet();
        }

        public override void RenameUses(Identifier orig, Identifier newIdentifier)
        {
            for (var i = 0; i < Function.UpValueBindings.Count; i++)
            {
                if (Function.UpValueBindings[i] == orig)
                {
                    Function.UpValueBindings[i] = newIdentifier;
                    newIdentifier.IsClosureBound = true;
                }
            }
        }

        public override string ToString()
        {
            /*string ret = "[";

            // Debug closure bindings
            for (int i = 0; i < UpvalueBindings.Count(); i++)
            {
                ret += UpvalueBindings[i].ToString();
                if (i != UpvalueBindings.Count() - 1)
                {
                    ret += ", ";
                }
            }
            ret += "] -> ";*/
            return Function.ToString();
        }
    }

    public class IdentifierReference : Expression
    {
        public Identifier Identifier;
        // Each entry represents a new level of indirection for multidimensional arrays
        public List<Expression> TableIndices = new List<Expression>();
        public bool DotNotation = false;

        public bool HasIndex { get => TableIndices.Count != 0; }

        public IdentifierReference(Identifier id)
        {
            Identifier = id;
        }

        public IdentifierReference(Identifier id, Expression index)
        {
            Identifier = id;
            TableIndices = new List<Expression> { index };
        }

        public override void Parenthesize()
        {
            foreach (var idx in TableIndices)
            {
                idx.Parenthesize();
            }
        }

        public override HashSet<Identifier> GetUses(bool registerOnly)
        {
            var ret = new HashSet<Identifier>();
            if ((!registerOnly || Identifier.Type == Identifier.IdentifierType.Register) && !Identifier.IsClosureBound)
            {
                ret.Add(Identifier);
            }
            foreach (var idx in TableIndices)
            {
                ret.UnionWith(idx.GetUses(registerOnly));
            }
            return ret;
        }

        public override void RenameUses(Identifier orig, Identifier newIdentifier)
        {
            if (Identifier == orig && !Identifier.IsClosureBound)
            {
                Identifier = newIdentifier;
                Identifier.UseCount++;
            }
            foreach (var idx in TableIndices)
            {
                idx.RenameUses(orig, newIdentifier);
            }
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            var changed = false;
            for (var i = 0; i < TableIndices.Count; i++)
            {
                if (ShouldReplace(orig, TableIndices[i]))
                {
                    TableIndices[i] = sub;
                    changed = true;
                }
                else
                {
                    changed = TableIndices[i].ReplaceUses(orig, sub);
                }
            }
            if (orig == Identifier && sub is IdentifierReference ir && ir.TableIndices.Count == 0)
            {
                Identifier = ir.Identifier;
                changed = true;
            }
            else if (orig == Identifier && sub is IdentifierReference ir2 && ir2.TableIndices.Count > 0)
            {
                Identifier = ir2.Identifier;
                var newl = new List<Expression>();
                newl.AddRange(ir2.TableIndices);
                newl.AddRange(TableIndices);
                TableIndices = newl;
                changed = true;
            }
            return changed;
        }

        public override List<Expression> GetExpressions()
        {
            var ret = new List<Expression> { this };
            foreach (var idx in TableIndices)
            {
                ret.AddRange(idx.GetExpressions());
            }
            return ret;
        }

        public override int GetLowestConstantId()
        {
            var id = Identifier.ConstantId;
            foreach (var idx in TableIndices)
            {
                var nid = idx.GetLowestConstantId();
                if (id == -1)
                {
                    id = nid;
                }
                else if (nid != -1)
                {
                    id = Math.Min(id, idx.GetLowestConstantId());
                }
            }
            return id;
        }

        public override string ToString()
        {
            // Detect a Lua 5.3 global variable and don't display it as a table reference
            var isGlobal = (Identifier.Type == Identifier.IdentifierType.GlobalTable);
            var ret = isGlobal ? "" : Identifier.ToString();
            foreach (var idx in TableIndices)
            {
                if (isGlobal && idx is Constant g && g.ConstType == Constant.ConstantType.ConstString)
                {
                    ret += g.String;
                    isGlobal = false;
                }
                else if (/*DotNotation && */idx is Constant c && c.ConstType == Constant.ConstantType.ConstString)
                {
                    ret += "." + c.String;
                }
                else
                {
                    ret += $@"[{idx.ToString()}]";
                }
            }
            return ret;
        }
    }

    public interface IOperator
    {
        int GetPrecedence();
        void SetHasParentheses(bool paren);
    }

    public class Concat : Expression, IOperator
    {
        public List<Expression> Exprs;
        private bool HasParentheses = false;

        public Concat(List<Expression> expr)
        {
            Exprs = expr;
        }

        public int GetPrecedence()
        {
            return 4;
        }

        public override void Parenthesize()
        {
            foreach (var expr in Exprs)
            {
                if (expr is IOperator op && op.GetPrecedence() > GetPrecedence())
                {
                    op.SetHasParentheses(true);
                }
            }
        }

        public override HashSet<Identifier> GetUses(bool registerOnly)
        {
            var ret = new HashSet<Identifier>();
            foreach (var arg in Exprs)
            {
                ret.UnionWith(arg.GetUses(registerOnly));
            }
            return ret;
        }

        public override void RenameUses(Identifier orig, Identifier newIdentifier)
        {
            foreach (var arg in Exprs)
            {
                arg.RenameUses(orig, newIdentifier);
            }
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            var replaced = false;
            for (var i = 0; i < Exprs.Count; i++)
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

        public override List<Expression> GetExpressions()
        {
            var ret = new List<Expression> { this };
            foreach(var exp in Exprs)
            {
                ret.AddRange(exp.GetExpressions());
            }
            return ret;
        }

        public void SetHasParentheses(bool paren)
        {
            HasParentheses = paren;
        }

        public override int GetLowestConstantId()
        {
            var id = int.MaxValue;
            foreach (var e in Exprs)
            {
                var nid = e.GetLowestConstantId();
                if (nid != -1)
                {
                    id = Math.Min(id, e.GetLowestConstantId());
                }
            }
            return id != int.MaxValue ? id : -1;
        }

        public override string ToString()
        {
            var ret = "";

            // Pattern match special lua this call
            if (HasParentheses)
            {
                ret += "(";
            }
            for (var i = 0; i < Exprs.Count; i++)
            {
                ret += Exprs[i].ToString();
                if (i != Exprs.Count - 1)
                {
                    ret += " .. ";
                }
            }
            if (HasParentheses)
            {
                ret += ")";
            }
            return ret;
        }
    }

    public class InitializerList : Expression
    {
        public List<Expression> Exprs;
        public List<Constant> Assignments = null;

        public InitializerList(List<Expression> expr)
        {
            Exprs = expr;
        }

        public override void Parenthesize()
        {
            Exprs.ForEach(x => x.Parenthesize());
        }

        public override HashSet<Identifier> GetUses(bool registerOnly)
        {
            var ret = new HashSet<Identifier>();
            foreach (var arg in Exprs)
            {
                ret.UnionWith(arg.GetUses(registerOnly));
            }
            return ret;
        }

        public override void RenameUses(Identifier orig, Identifier newIdentifier)
        {
            foreach (var arg in Exprs)
            {
                arg.RenameUses(orig, newIdentifier);
            }
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            var replaced = false;
            for (var i = 0; i < Exprs.Count; i++)
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

        public override List<Expression> GetExpressions()
        {
            var ret = new List<Expression> { this };
            foreach (var exp in Exprs)
            {
                ret.AddRange(exp.GetExpressions());
            }
            return ret;
        }

        public override int GetLowestConstantId()
        {
            var id = int.MaxValue;
            foreach (var e in Exprs)
            {
                var nid = e.GetLowestConstantId();
                if (nid != -1)
                {
                    id = Math.Min(id, e.GetLowestConstantId());
                }
            }
            return id != int.MaxValue ? id : -1;
        }

        public override string ToString()
        {
            var ret = "{";

            // Pattern match special lua this call
            for (var i = 0; i < Exprs.Count; i++)
            {
                if (Assignments != null)
                {
                    ret += Assignments[i].String + " = ";
                }
                ret += Exprs[i].ToString();
                if (i != Exprs.Count - 1)
                {
                    ret += ", ";
                }
            }
            ret += "}";
            return ret;
        }
    }

    public class BinOp : Expression, IOperator
    {
        public enum OperationType
        {
            OpAdd,
            OpSub,
            OpMul,
            OpDiv,
            OpFloorDiv,
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
            OpBAnd,
            OpBOr,
            OpBXOr,
            OpShiftRight,
            OpShiftLeft,
            OpLoopCompare,
        }

        public Expression Left;
        public Expression Right;
        public OperationType Operation;

        private bool _hasParentheses = false;

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

        /// <summary>
        /// The lower the number the higher the precedence
        /// </summary>
        /// <returns></returns>
        public int GetPrecedence()
        {
            switch (Operation)
            {
                case OperationType.OpPow:
                    return 0;
                case OperationType.OpMul:
                case OperationType.OpDiv:
                case OperationType.OpFloorDiv:
                case OperationType.OpMod:
                    return 2;
                case OperationType.OpAdd:
                case OperationType.OpSub:
                    return 3;
                case OperationType.OpShiftRight:
                case OperationType.OpShiftLeft:
                    return 4;
                case OperationType.OpBAnd:
                    return 5;
                case OperationType.OpBXOr:
                    return 6;
                case OperationType.OpBOr:
                    return 7;
                case OperationType.OpEqual:
                case OperationType.OpNotEqual:
                case OperationType.OpLessThan:
                case OperationType.OpLessEqual:
                case OperationType.OpGreaterThan:
                case OperationType.OpGreaterEqual:
                case OperationType.OpLoopCompare:
                    return 8;
                case OperationType.OpAnd:
                    return 9;
                case OperationType.OpOr:
                    return 10;
                default:
                    return 99999;
            }
        }

        public override void Parenthesize()
        {
            // If left has a lower precedence than this op, then add parantheses to evaluate it first
            if (Left is IOperator op1 && op1.GetPrecedence() > GetPrecedence())
            {
                op1.SetHasParentheses(true);
            }
            if (Right is IOperator op2 && op2.GetPrecedence() > GetPrecedence())
            {
                op2.SetHasParentheses(true);
            }

            // If we're a comparison op, we may need to swap the left and right if they both refer to constants
            var leftConstId = Left.GetLowestConstantId();
            var rightConstId = Right.GetLowestConstantId();

            if (IsCompare() && Operation != OperationType.OpLoopCompare && 
                leftConstId != -1 && rightConstId != -1 && leftConstId > rightConstId)
            {
                // We need to swap the left and right to keep matching recompiles
                (Right, Left) = (Left, Right);
                Operation = Operation switch
                {
                    OperationType.OpLessThan => OperationType.OpGreaterThan,
                    OperationType.OpGreaterThan => OperationType.OpLessThan,
                    OperationType.OpLessEqual => OperationType.OpGreaterEqual,
                    OperationType.OpGreaterEqual => OperationType.OpLessEqual,
                    _ => Operation
                };
            }

            Left.Parenthesize();
            Right.Parenthesize();
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

        public override HashSet<Identifier> GetUses(bool registerOnly)
        {
            var ret = new HashSet<Identifier>();
            ret.UnionWith(Left.GetUses(registerOnly));
            ret.UnionWith(Right.GetUses(registerOnly));
            return ret;
        }

        public override void RenameUses(Identifier orig, Identifier newIdentifier)
        {
            Left.RenameUses(orig, newIdentifier);
            Right.RenameUses(orig, newIdentifier);
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            var replaced = false;
            if (ShouldReplace(orig, Left))
            {
                Left = sub;
                replaced = true;
            }
            else
            {
                replaced = Left.ReplaceUses(orig, sub);
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

        public override List<Expression> GetExpressions()
        {
            var ret = new List<Expression> { this };
            ret.AddRange(Left.GetExpressions());
            ret.AddRange(Right.GetExpressions());
            return ret;
        }

        public override int GetLowestConstantId()
        {
            var left = Left.GetLowestConstantId();
            var right = Right.GetLowestConstantId();
            if (left == -1)
            {
                return right;
            }
            if (right == -1)
            {
                return left;
            }
            return Math.Min(left, right);
        }

        public override string ToString()
        {
            var op = Operation switch
            {
                OperationType.OpAdd => "+",
                OperationType.OpDiv => "/",
                OperationType.OpFloorDiv => "//",
                OperationType.OpMod => "%",
                OperationType.OpMul => "*",
                OperationType.OpPow => "^",
                OperationType.OpSub => "-",
                OperationType.OpEqual => "==",
                OperationType.OpNotEqual => "~=",
                OperationType.OpLessThan => "<",
                OperationType.OpLessEqual => "<=",
                OperationType.OpGreaterThan => ">",
                OperationType.OpGreaterEqual => ">=",
                OperationType.OpAnd => "and",
                OperationType.OpOr => "or",
                OperationType.OpBAnd => "&",
                OperationType.OpBOr => "|",
                OperationType.OpBXOr => "~",
                OperationType.OpShiftRight => ">>",
                OperationType.OpShiftLeft => "<<",
                OperationType.OpLoopCompare => ">?=",
                _ => ""
            };
            var ret = "";
            if (_hasParentheses)
            {
                ret += "(";
            }
            ret += $@"{Left} {op} {Right}";
            if (_hasParentheses)
            {
                ret += ")";
            }
            return ret;
        }

        public void SetHasParentheses(bool paren)
        {
            _hasParentheses = paren;
        }
    }

    public class UnaryOp : Expression, IOperator
    {
        public enum OperationType
        {
            OpNegate,
            OpNot,
            OpBNot,
            OpLength,
        }

        public Expression Exp;
        public OperationType Operation;

        private bool _hasParentheses = false;

        public UnaryOp(Expression exp, OperationType op)
        {
            Exp = exp;
            Operation = op;
        }

        public override HashSet<Identifier> GetUses(bool registerOnly)
        {
            var ret = new HashSet<Identifier>();
            ret.UnionWith(Exp.GetUses(registerOnly));
            return ret;
        }

        public override void RenameUses(Identifier orig, Identifier newIdentifier)
        {
            Exp.RenameUses(orig, newIdentifier);
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            if (ShouldReplace(orig, Exp))
            {
                Exp = sub;
                return true;
            }

            return Exp.ReplaceUses(orig, sub);
        }

        public override List<Expression> GetExpressions()
        {
            var ret = new List<Expression> { this };
            ret.AddRange(Exp.GetExpressions());
            return ret;
        }

        public override int GetLowestConstantId()
        {
            return Exp.GetLowestConstantId();
        }
        public override string ToString()
        {
            var op = Operation switch
            {
                OperationType.OpNegate => "-",
                OperationType.OpNot => "not ",
                OperationType.OpBNot => "~",
                OperationType.OpLength => "#",
                _ => ""
            };
            var ret = "";
            if (_hasParentheses)
            {
                ret += "(";
            }
            ret += $@"{op}{Exp}";
            if (_hasParentheses)
            {
                ret += ")";
            }
            return ret;
        }

        public int GetPrecedence()
        {
            return 1;
        }
        public override void Parenthesize()
        {
            // If left has a lower precedence than this op, then add parantheses to evaluate it first
            if (Exp is IOperator op1 && op1.GetPrecedence() > GetPrecedence())
            {
                op1.SetHasParentheses(true);
            }
            Exp.Parenthesize();
        }

        public void SetHasParentheses(bool paren)
        {
            _hasParentheses = paren;
        }
    }

    public class FunctionCall : Expression
    {
        public Expression Function;
        public List<Expression> Args;

        public uint BeginArg = 0;
        
        /// <summary>
        /// Set to true if the number of values returned from this call isn't explicitly stated in the opcode and
        /// needs analysis to resolve.
        /// </summary>
        public bool HasAmbiguousReturnCount = false;
        
        /// <summary>
        /// Set to true if the number of arguments for this call isn't explicitly stated in the opcode and
        /// needs analysis to resolve.
        /// </summary>
        public bool HasAmbiguousArgumentCount = false;

        /// <summary>
        /// Index of where the function def register was originally defined. Used to help decide what expressions to inline
        /// </summary>
        public int FunctionDefIndex = 0;

        public FunctionCall(Expression fun, List<Expression> args)
        {
            Function = fun;
            Args = args;
        }

        public override void Parenthesize()
        {
            Function.Parenthesize();
            Args.ForEach(x => x.Parenthesize());
        }

        public override HashSet<Identifier> GetUses(bool registerOnly)
        {
            var ret = new HashSet<Identifier>();
            foreach (var arg in Args)
            {
                ret.UnionWith(arg.GetUses(registerOnly));
            }
            ret.UnionWith(Function.GetUses(registerOnly));
            return ret;
        }

        public override void RenameUses(Identifier orig, Identifier newIdentifier)
        {
            Function.RenameUses(orig, newIdentifier);
            foreach (var arg in Args)
            {
                arg.RenameUses(orig, newIdentifier);
            }
        }

        public override bool ReplaceUses(Identifier orig, Expression sub)
        {
            bool replaced;
            if (ShouldReplace(orig, Function) && sub is IdentifierReference or Constant)
            {
                Function = sub;
                replaced = true;
            }
            else
            {
                replaced = Function.ReplaceUses(orig, sub);
            }
            for (var i = 0; i < Args.Count; i++)
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

        public override List<Expression> GetExpressions()
        {
            var ret = new List<Expression> { this };
            foreach (var exp in Args)
            {
                ret.AddRange(exp.GetExpressions());
            }
            ret.AddRange(Function.GetExpressions());
            return ret;
        }

        public override int GetLowestConstantId()
        {
            var id = Function.GetLowestConstantId();
            foreach (var idx in Args)
            {
                var nid = idx.GetLowestConstantId();
                if (id == -1)
                {
                    id = nid;
                }
                else if (nid != -1)
                {
                    id = Math.Min(id, idx.GetLowestConstantId());
                }
            }
            return id;
        }

        public override string ToString()
        {
            var ret = "";

            // Pattern match special lua this call
            var beginArg = 0;
            if (Function is IdentifierReference ir && ir.TableIndices.Count == 1 &&
                ir.TableIndices[0] is Constant { ConstType: Constant.ConstantType.ConstString } c &&
                ir.Identifier.Type != Identifier.IdentifierType.GlobalTable)
            {
                if (Args.Count >= 1 && Args[0] is IdentifierReference thisIdentifier && 
                    thisIdentifier.TableIndices.Count == 0 && thisIdentifier.Identifier == ir.Identifier)
                {
                    ret += $@"{ir.Identifier}:{c.String}(";
                    beginArg = 1;
                }
                else
                {
                    ret += $@"{ir.Identifier}.{c.String}(";
                }
            }
            else if (Function is IdentifierReference ir2 && ir2.TableIndices.Count == 2 &&
                     ir2.Identifier.Type == Identifier.IdentifierType.GlobalTable &&
                     ir2.TableIndices[1] is Constant { ConstType: Constant.ConstantType.ConstString } c2 &&
                     ir2.TableIndices[0] is Constant { ConstType: Constant.ConstantType.ConstString } c3 &&
                     Args.Count >= 1 && Args[0] is IdentifierReference thisIdentifier && 
                     thisIdentifier.TableIndices.Count == 1 && thisIdentifier.Identifier == ir2.Identifier && 
                     ir2.TableIndices[0] is Constant { ConstType: Constant.ConstantType.ConstString } c4 && 
                     c3.String == c4.String)
            {
                ret += $@"{c3.String}:{c2.String}(";
                beginArg = 1;
            }
            else
            {
                ret += Function + "(";
            }
            for (var i = beginArg; i < Args.Count; i++)
            {
                ret += Args[i].ToString();
                if (i != Args.Count - 1)
                {
                    ret += ", ";
                }
            }
            ret += ")";
            return ret;
        }
    }
}
