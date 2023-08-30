using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LuaDecompilerCore.IR
{
    /// <summary>
    /// Base class for an expression, which can basically do anything expressive
    /// </summary>
    public abstract class Expression : IMatchable
    {
        public virtual HashSet<Identifier> GetUses(HashSet<Identifier> uses, bool registersOnly)
        {
            return uses;
        }
        
        public HashSet<Identifier> GetUses(bool registersOnly)
        {
            var uses = new HashSet<Identifier>(5);
            GetUses(uses, registersOnly);
            return uses;
        }

        public virtual void RenameUses(Identifier original, Identifier newIdentifier) { }

        public static bool ShouldReplace(Identifier orig, Expression cand)
        {
            return cand is IdentifierReference { TableIndices.Count: 0 } ident && ident.Identifier == orig;
        }

        public virtual bool ReplaceUses(Identifier original, Expression sub) { return false; }

        public virtual int UseCount(Identifier use) { return 0; }

        public virtual void Parenthesize() { }

        public virtual List<Expression> GetExpressions()
        {
            return new List<Expression> { this };
        }

        public virtual int GetLowestConstantId()
        {
            return -1;
        }

        public override string ToString()
        {
            return FunctionPrinter.DebugPrintExpression(this);
        }

        public abstract bool MatchAny(Func<IMatchable, bool> condition);
    }

    /// <summary>
    /// Empty expression that has nothing and prints nothing. Mostly used for empty expressions in control flow
    /// structures
    /// </summary>
    public sealed class EmptyExpression : Expression
    {
        public override bool MatchAny(Func<IMatchable, bool> condition)
        {
            return condition.Invoke(this);
        }
    }

    public sealed class Constant : Expression
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

        public readonly ConstantType ConstType;
        public readonly double Number;
        public readonly ulong Integer;
        public readonly string String = "";
        public readonly bool Boolean;

        public readonly int ConstantId;

        public Constant(double number, int id)
        {
            ConstType = ConstantType.ConstNumber;
            Number = number;
            ConstantId = id;
        }

        public Constant(ulong integer, int id)
        {
            ConstType = ConstantType.ConstInteger;
            Integer = integer;
            ConstantId = id;
        }

        public Constant(string str, int id)
        {
            ConstType = ConstantType.ConstString;
            String = str;
            ConstantId = id;
        }

        public Constant(bool b, int id)
        {
            ConstType = ConstantType.ConstBool;
            Boolean = b;
            ConstantId = id;
        }

        public Constant(ConstantType type, int id)
        {
            ConstType = type;
            ConstantId = id;
        }

        public override int GetLowestConstantId()
        {
            return ConstantId;
        }

        public override bool MatchAny(Func<IMatchable, bool> condition)
        {
            return condition.Invoke(this);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Constant other || ConstType != other.ConstType)
                return false;

            return ConstType switch
            {
                ConstantType.ConstNumber => Number == other.Number,
                ConstantType.ConstInteger => Integer == other.Integer,
                ConstantType.ConstString => String == other.String,
                ConstantType.ConstBool => Boolean == other.Boolean,
                ConstantType.ConstTable => true,
                ConstantType.ConstVarargs => true,
                ConstantType.ConstNil => true,
                _ => false
            };
        }

        public override int GetHashCode()
        {
            return ConstType switch
            {
                ConstantType.ConstNumber => Number.GetHashCode(),
                ConstantType.ConstInteger => Integer.GetHashCode(),
                ConstantType.ConstString => String.GetHashCode(),
                ConstantType.ConstBool => Boolean.GetHashCode(),
                ConstantType.ConstTable => ConstType.GetHashCode(),
                ConstantType.ConstVarargs => ConstType.GetHashCode(),
                ConstantType.ConstNil => ConstType.GetHashCode(),
                _ => 0
            };
        }
    }

    public sealed class Closure : Expression
    {
        public readonly Function Function;

        public Closure(Function fun)
        {
            Function = fun;
        }

        public override HashSet<Identifier> GetUses(HashSet<Identifier> uses, bool registersOnly)
        {
            foreach (var binding in Function.UpValueBindings)
            {
                if (!registersOnly || binding.IsRegister)
                    uses.Add(binding);
            }

            return uses;
        }

        public override void RenameUses(Identifier original, Identifier newIdentifier)
        {
            for (var i = 0; i < Function.UpValueBindings.Count; i++)
            {
                if (Function.UpValueBindings[i] == original)
                {
                    Function.UpValueBindings[i] = newIdentifier;
                }
            }
        }

        public override int UseCount(Identifier use)
        {
            // Bit of a hack but multiplying the use count by 2 ensures that the identifier gets marked as a local and
            // expression propagation doesn't try to inline.
            return Function.UpValueBindings.Count(binding => use == binding) * 2;
        }

        public override bool MatchAny(Func<IMatchable, bool> condition)
        {
            return condition.Invoke(this);
        }
    }

    public sealed class IdentifierReference : Expression
    {
        public Identifier Identifier;
        // Each entry represents a new level of indirection for multidimensional arrays
        public List<Expression> TableIndices = new();

        public bool HasIndex => TableIndices.Count != 0;

        public bool HasSingleIndex => TableIndices.Count == 1;
        public Expression TableIndex => TableIndices[0];

        /// <summary>
        /// This identifier reference comes from a SELF op and should be printed with this (':') semantics
        /// </summary>
        public bool IsSelfReference = false;

        public IdentifierReference(Identifier id)
        {
            Identifier = id;
        }

        public IdentifierReference(Identifier id, Expression index)
        {
            Identifier = id;
            TableIndices = new List<Expression>(1) { index };
        }

        public override void Parenthesize()
        {
            foreach (var idx in TableIndices)
            {
                idx.Parenthesize();
            }
        }

        public override HashSet<Identifier> GetUses(HashSet<Identifier> uses, bool registersOnly)
        {
            if (!registersOnly || Identifier.IsRegister)
            {
                uses.Add(Identifier);
            }
            foreach (var idx in TableIndices)
            {
                idx.GetUses(uses, registersOnly);
            }

            return uses;
        }

        public override void RenameUses(Identifier original, Identifier newIdentifier)
        {
            if (Identifier == original)
            {
                Identifier = newIdentifier;
            }
            foreach (var idx in TableIndices)
            {
                idx.RenameUses(original, newIdentifier);
            }
        }

        public override bool ReplaceUses(Identifier original, Expression sub)
        {
            var changed = false;
            for (var i = 0; i < TableIndices.Count; i++)
            {
                if (ShouldReplace(original, TableIndices[i]))
                {
                    TableIndices[i] = sub;
                    changed = true;
                }
                else
                {
                    changed = changed || TableIndices[i].ReplaceUses(original, sub);
                }
            }
            if (original == Identifier && sub is IdentifierReference ir && ir.TableIndices.Count == 0)
            {
                Identifier = ir.Identifier;
                changed = true;
            }
            else if (original == Identifier && sub is IdentifierReference ir2 && ir2.TableIndices.Count > 0)
            {
                Identifier = ir2.Identifier;
                var newTableIndices = new List<Expression>();
                newTableIndices.AddRange(ir2.TableIndices);
                newTableIndices.AddRange(TableIndices);
                TableIndices = newTableIndices;
                changed = true;
            }
            return changed;
        }

        public override int UseCount(Identifier use)
        {
            var count = TableIndices.Sum(i => i.UseCount(use));
            return count + (Identifier == use ? 1 : 0);
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
            var id = Identifier.IsGlobal ? (int)Identifier.ConstantId : 0;
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

        public override bool MatchAny(Func<IMatchable, bool> condition)
        {
            var result = condition.Invoke(this);
            foreach (var idx in TableIndices)
            {
                result = result || idx.MatchAny(condition);
            }
            return result;
        }
    }

    public interface IOperator
    {
        int GetPrecedence();
        void SetHasParentheses(bool paren);
    }

    public sealed class Concat : Expression, IOperator
    {
        public readonly List<Expression> Expressions;
        public bool HasParentheses;

        public Concat(List<Expression> expression)
        {
            Expressions = expression;
        }

        public int GetPrecedence()
        {
            return 4;
        }

        public override void Parenthesize()
        {
            foreach (var expr in Expressions)
            {
                if (expr is IOperator op && op.GetPrecedence() > GetPrecedence())
                {
                    op.SetHasParentheses(true);
                }
            }
        }

        public override HashSet<Identifier> GetUses(HashSet<Identifier> uses, bool registersOnly)
        {
            foreach (var arg in Expressions)
            {
                arg.GetUses(uses, registersOnly);
            }

            return uses;
        }

        public override void RenameUses(Identifier original, Identifier newIdentifier)
        {
            foreach (var arg in Expressions)
            {
                arg.RenameUses(original, newIdentifier);
            }
        }

        public override bool ReplaceUses(Identifier original, Expression sub)
        {
            var replaced = false;
            for (var i = 0; i < Expressions.Count; i++)
            {
                if (ShouldReplace(original, Expressions[i]))
                {
                    Expressions[i] = sub;
                    replaced = true;
                }
                else
                {
                    replaced = replaced || Expressions[i].ReplaceUses(original, sub);
                }
            }
            return replaced;
        }

        public override int UseCount(Identifier use)
        {
            return Expressions.Sum(e => e.UseCount(use));
        }

        public override List<Expression> GetExpressions()
        {
            var ret = new List<Expression> { this };
            foreach(var exp in Expressions)
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
            foreach (var e in Expressions)
            {
                var nid = e.GetLowestConstantId();
                if (nid != -1)
                {
                    id = Math.Min(id, e.GetLowestConstantId());
                }
            }
            return id != int.MaxValue ? id : -1;
        }

        public override bool MatchAny(Func<IMatchable, bool> condition)
        {
            var result = condition.Invoke(this);
            foreach (var idx in Expressions)
            {
                result = result || idx.MatchAny(condition);
            }
            return result;
        }
    }

    public sealed class InitializerList : Expression
    {
        public readonly List<Expression> Expressions;
        public readonly List<Constant> Assignments = new();

        public bool ExpressionsEmpty => Expressions.Count == 0;
        public bool HasExpressions => Expressions.Count > 0;

        public InitializerList(List<Expression> expression)
        {
            Expressions = expression;
        }

        public override void Parenthesize()
        {
            Expressions.ForEach(x => x.Parenthesize());
        }

        public override HashSet<Identifier> GetUses(HashSet<Identifier> uses, bool registersOnly)
        {
            foreach (var arg in Expressions)
            {
                arg.GetUses(uses, registersOnly);
            }

            return uses;
        }

        public override void RenameUses(Identifier original, Identifier newIdentifier)
        {
            foreach (var arg in Expressions)
            {
                arg.RenameUses(original, newIdentifier);
            }
        }

        public override bool ReplaceUses(Identifier original, Expression sub)
        {
            var replaced = false;
            for (var i = 0; i < Expressions.Count; i++)
            {
                if (ShouldReplace(original, Expressions[i]))
                {
                    Expressions[i] = sub;
                    replaced = true;
                }
                else
                {
                    replaced = replaced || Expressions[i].ReplaceUses(original, sub);
                }
            }
            return replaced;
        }

        public override int UseCount(Identifier use)
        {
            return Expressions.Sum(e => e.UseCount(use));
        }

        public override List<Expression> GetExpressions()
        {
            var ret = new List<Expression> { this };
            foreach (var expression in Expressions)
            {
                ret.AddRange(expression.GetExpressions());
            }
            return ret;
        }

        public override int GetLowestConstantId()
        {
            var id = int.MaxValue;
            foreach (var expression in Expressions)
            {
                var nid = expression.GetLowestConstantId();
                if (nid != -1)
                {
                    id = Math.Min(id, expression.GetLowestConstantId());
                }
            }
            return id != int.MaxValue ? id : -1;
        }

        public override bool MatchAny(Func<IMatchable, bool> condition)
        {
            var result = condition.Invoke(this);
            foreach (var idx in Expressions)
            {
                result = result || idx.MatchAny(condition);
            }
            return result;
        }
    }

    public sealed class BinOp : Expression, IOperator
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
            OpBxOr,
            OpShiftRight,
            OpShiftLeft,
            OpLoopCompare,
        }

        public Expression Left;
        public Expression Right;
        public OperationType Operation;

        /// <summary>
        /// Whether or not this bin-op comparison came from a LT_BK or LE_BK instruction and has position requirements
        /// for the left constant.
        /// </summary>
        public bool IsBkComparison { get; private set; }
        public bool HasParentheses { get; private set; }

        public bool IsNonEqualityComparison => Operation is OperationType.OpLessThan or OperationType.OpLessEqual
            or OperationType.OpGreaterThan or OperationType.OpGreaterEqual;

        public bool IsBkLegal => IsBkComparison ? Left is Constant : Left is not Constant;

        public BinOp(Expression left, Expression right, OperationType op, bool isBkComparison = false)
        {
            Left = left;
            Right = right;
            Operation = op;
            IsBkComparison = isBkComparison;
        }

        /// <summary>
        /// Negates this op as a single condition
        /// </summary>
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
                case OperationType.OpAdd:
                case OperationType.OpSub:
                case OperationType.OpMul:
                case OperationType.OpDiv:
                case OperationType.OpFloorDiv:
                case OperationType.OpMod:
                case OperationType.OpPow:
                case OperationType.OpAnd:
                case OperationType.OpOr:
                case OperationType.OpBAnd:
                case OperationType.OpBOr:
                case OperationType.OpBxOr:
                case OperationType.OpShiftRight:
                case OperationType.OpShiftLeft:
                default:
                    throw new Exception("Attempting to negate non-conditional binary operation");
            }
            return this;
        }

        /// <summary>
        /// Negates this op as a conditional expression that may be built of multiple parts
        /// </summary>
        public bool NegateConditionalExpression()
        {
            // Lastly if left and right are binops we negate them
            bool leftNegated = false;
            if (Left is BinOp leftBinOp)
                leftNegated = leftBinOp.NegateConditionalExpression();
            bool rightNegated = false;
            if (Right is BinOp rightBinOp)
                rightNegated = rightBinOp.NegateConditionalExpression();
            
            // Comparison operations we just do simple negation
            if (Operation is OperationType.OpEqual or OperationType.OpNotEqual or OperationType.OpLessThan or
                OperationType.OpLessEqual or OperationType.OpGreaterThan or OperationType.OpGreaterEqual)
                NegateCondition();
            else if (Operation is OperationType.OpAnd or OperationType.OpOr)
            {
                Operation = Operation switch
                {
                    OperationType.OpAnd => OperationType.OpOr,
                    OperationType.OpOr => OperationType.OpAnd,
                    _ => Operation
                };

                if (Left is UnaryOp leftUnaryOp)
                {
                    Left = leftUnaryOp.NegateConditionalExpression();
                }
                else if (!leftNegated)
                {
                    Left = new UnaryOp(Left, UnaryOp.OperationType.OpNot);
                }
                
                if (Right is UnaryOp rightUnaryOp)
                {
                    Right = rightUnaryOp.NegateConditionalExpression();
                }
                else if (!rightNegated)
                {
                    Right = new UnaryOp(Right, UnaryOp.OperationType.OpNot);
                }
            }
            else
            {
                return false;
            }

            return true;
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
                case OperationType.OpBxOr:
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
            // If left has a lower precedence than this op, then add parentheses to evaluate it first
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

            if (IsCompare && Operation is not OperationType.OpLoopCompare and not OperationType.OpEqual 
                    and not OperationType.OpNotEqual && 
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

        public bool IsCompare => Operation switch
        {
            OperationType.OpEqual => true,
            OperationType.OpNotEqual => true,
            OperationType.OpLessThan => true,
            OperationType.OpLessEqual => true,
            OperationType.OpGreaterThan => true,
            OperationType.OpGreaterEqual => true,
            OperationType.OpLoopCompare => true,
            _ => false
        };

        public override HashSet<Identifier> GetUses(HashSet<Identifier> uses, bool registersOnly)
        {
            Left.GetUses(uses, registersOnly);
            Right.GetUses(uses, registersOnly);
            return uses;
        }

        public override void RenameUses(Identifier original, Identifier newIdentifier)
        {
            Left.RenameUses(original, newIdentifier);
            Right.RenameUses(original, newIdentifier);
        }

        public override bool ReplaceUses(Identifier original, Expression sub)
        {
            bool replaced;
            if (ShouldReplace(original, Left))
            {
                Left = sub;
                replaced = true;
            }
            else
            {
                replaced = Left.ReplaceUses(original, sub);
            }
            if (ShouldReplace(original, Right))
            {
                Right = sub;
                replaced = true;
            }
            else
            {
                replaced = replaced || Right.ReplaceUses(original, sub);
            }
            return replaced;
        }

        public override int UseCount(Identifier use)
        {
            return Left.UseCount(use) + Right.UseCount(use);
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

        public override bool MatchAny(Func<IMatchable, bool> condition)
        {
            var result = condition.Invoke(this);
            result = result || Left.MatchAny(condition) || Right.MatchAny(condition);
            return result;
        }

        public void SetHasParentheses(bool paren)
        {
            HasParentheses = paren;
        }
    }

    public sealed class UnaryOp : Expression, IOperator
    {
        public enum OperationType
        {
            OpNegate,
            OpNot,
            OpBNot,
            OpLength,
        }

        public Expression Expression;
        public readonly OperationType Operation;

        public bool HasParentheses { get; private set; }

        public readonly bool Preserve;

        public UnaryOp(Expression expression, OperationType op, bool preserve = false)
        {
            Expression = expression;
            Operation = op;
            Preserve = preserve;
        }

        public override HashSet<Identifier> GetUses(HashSet<Identifier> uses, bool registersOnly)
        {
            Expression.GetUses(uses, registersOnly);
            return uses;
        }

        public override void RenameUses(Identifier original, Identifier newIdentifier)
        {
            Expression.RenameUses(original, newIdentifier);
        }

        public override bool ReplaceUses(Identifier original, Expression sub)
        {
            if (ShouldReplace(original, Expression))
            {
                Expression = sub;
                return true;
            }

            return Expression.ReplaceUses(original, sub);
        }

        public override int UseCount(Identifier use)
        {
            return Expression.UseCount(use);
        }

        public override List<Expression> GetExpressions()
        {
            var ret = new List<Expression> { this };
            ret.AddRange(Expression.GetExpressions());
            return ret;
        }
        
        /// <summary>
        /// Negates this op as a conditional expression that may be built of multiple parts. Since this op may result
        /// in the unary op needing to be removed, the expression returned will indicate what this needs to be replaced
        /// with.
        /// </summary>
        public Expression NegateConditionalExpression()
        {
            if (Operation == OperationType.OpNot && Preserve)
                return new UnaryOp(this, OperationType.OpNot);
            return Operation == OperationType.OpNot ? Expression : this;
        }

        public override int GetLowestConstantId()
        {
            return Expression.GetLowestConstantId();
        }

        public override bool MatchAny(Func<IMatchable, bool> condition)
        {
            var result = condition.Invoke(this);
            result = result || Expression.MatchAny(condition);
            return result;
        }

        public int GetPrecedence()
        {
            return 1;
        }
        public override void Parenthesize()
        {
            // If left has a lower precedence than this op, then add parantheses to evaluate it first
            if (Expression is IOperator op1 && op1.GetPrecedence() > GetPrecedence())
            {
                op1.SetHasParentheses(true);
            }
            Expression.Parenthesize();
        }

        public void SetHasParentheses(bool paren)
        {
            HasParentheses = paren;
        }
    }

    public sealed class FunctionCall : Expression
    {
        public Expression Function;
        public readonly List<Expression> Args;

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
        public int FunctionDefIndex = -1;

        /// <summary>
        /// Call is a "this" call where the first argument is implicit
        /// </summary>
        public bool IsThisCall = false;

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

        public override HashSet<Identifier> GetUses(HashSet<Identifier> uses, bool registersOnly)
        {
            foreach (var arg in Args)
            {
                arg.GetUses(uses, registersOnly);
            }
            Function.GetUses(uses, registersOnly);
            return uses;
        }

        public override void RenameUses(Identifier original, Identifier newIdentifier)
        {
            Function.RenameUses(original, newIdentifier);
            foreach (var arg in Args)
            {
                arg.RenameUses(original, newIdentifier);
            }
        }

        public override bool ReplaceUses(Identifier original, Expression sub)
        {
            bool replaced;
            if (ShouldReplace(original, Function) && sub is IdentifierReference or Constant or FunctionCall)
            {
                if (sub is IdentifierReference { IsSelfReference: true })
                {
                    IsThisCall = true;
                }
                Function = sub;
                replaced = true;
            }
            else
            {
                replaced = Function.ReplaceUses(original, sub);
            }
            for (var i = 0; i < Args.Count; i++)
            {
                if (ShouldReplace(original, Args[i]))
                {
                    Args[i] = sub;
                    replaced = true;
                }
                else
                {
                    replaced = replaced || Args[i].ReplaceUses(original, sub);
                }
            }
            return replaced;
        }

        public override int UseCount(Identifier use)
        {
            return Args.Sum(a => a.UseCount(use)) + Function.UseCount(use);
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

        public override bool MatchAny(Func<IMatchable, bool> condition)
        {
            var result = condition.Invoke(this);
            foreach (var idx in Args)
            {
                result = result || idx.MatchAny(condition);
            }

            result = result || Function.MatchAny(condition);
            return result;
        }
    }
}
