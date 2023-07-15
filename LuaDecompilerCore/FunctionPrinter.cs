using System;
using System.CodeDom.Compiler;
using System.Globalization;
using System.Linq;
using System.Text;
using LuaDecompilerCore.CFG;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore;

public class FunctionPrinter : IIrVisitor
{
    private StringBuilder _builder;
    private int _indentLevel = 0;

    private bool _printInfiniteLoop = false;
    private string _functionName = null;
    
    public FunctionPrinter()
    {
        
    }

    public string PrintFunction(Function function)
    {
        _builder = new StringBuilder();
        VisitFunction(function);
        return _builder.ToString();
    }
    
    public void PrintFunctionToStringBuilder(Function function, StringBuilder builder)
    {
        _builder = builder;
        VisitFunction(function);
    }

    private void Indent()
    {
        for (var i = 0; i < _indentLevel; i++)
        { 
            _builder.Append("    ");
        }
    }
    
    public void VisitFunction(Function function)
    {
        if (function.FunctionId != 0)
        {
            if (_functionName == null)
            {
                //string str = $@"function {DebugID} (";
                _builder.Append(@"function (");
            }
            else
            {
                //str = $@"function {DebugID} {funname}(";
                _builder.Append($@"function {_functionName}(");
            }
            for (var i = 0; i < function.Parameters.Count; i++)
            {
                function.Parameters[i].Accept(this);
                if (i != function.Parameters.Count - 1)
                {
                    _builder.Append(", ");
                }
            }
            if (function.IsVarargs)
            {
                if (function.Parameters.Count > 0)
                {
                    _builder.Append(", ...");
                }
                else
                {
                    _builder.Append("...");
                }
            }
            _builder.Append(")\n");
            _indentLevel += 1;
        }
        if (function.IsAst)
        {
            if (function.InsertDebugComments)
            {
                for (var i = 0; i < _indentLevel; i++)
                {
                    _builder.Append("    ");
                }
                _builder.Append($"-- Function ID = {function.FunctionId}\n");
            }
            function.BeginBlock.Accept(this);
            _builder.Append('\n');
        }
        else
        {
            // Traverse the basic blocks ordered by their ID
            foreach (var b in function.BlockList.OrderBy(a => a.BlockID))
            {
                if (b == function.EndBlock && b != function.BeginBlock)
                {
                    continue;
                }
                for (var i = 0; i < _indentLevel; i++)
                {
                    if (i == _indentLevel - 1)
                    {
                        _builder.Append("  ");
                        continue;
                    }
                    _builder.Append("    ");
                }
                _builder.Append(b.ToStringWithLoop() + "\n");
                foreach (var inst in b.PhiFunctions.Values)
                {
                    Indent();
                    inst.Accept(this);
                    _builder.Append('\n');
                }
                foreach (var inst in b.Instructions)
                {
                    for (var i = 0; i < _indentLevel; i++)
                    {
                        if (inst is Label && i == _indentLevel - 1)
                        {
                            _builder.Append("  ");
                            continue;
                        }
                        _builder.Append("    ");
                    }

                    inst.Accept(this);
                    _builder.Append('\n');
                }

                // Insert an implicit goto for fallthrough blocks if the destination isn't actually the next block
                var lastInstruction = (b.Instructions.Count > 0) ? b.Instructions.Last() : null;
                if (lastInstruction != null && 
                    ((lastInstruction is Jump { Conditional: true } && 
                      b.Successors[0].BlockID != b.BlockID + 1) ||
                     (lastInstruction is not Jump && lastInstruction is not Return && 
                      b.Successors[0].BlockID != (b.BlockID + 1))))
                {
                    for (var i = 0; i < _indentLevel; i++)
                    {
                        _builder.Append("    ");
                    }

                    _builder.Append("(goto ");
                    _builder.Append(b.Successors[0]);
                    _builder.Append(")\n");
                }
            }
        }
        if (function.FunctionId != 0)
        {
            _indentLevel -= 1;
            for (var i = 0; i < _indentLevel; i++)
            {
                _builder.Append("    ");
            }
            _builder.Append("end\n");
        }

        _functionName = null;
    }

    public void VisitBasicBlock(BasicBlock basicBlock)
    {
        var count = basicBlock.IsInfiniteLoop && !_printInfiniteLoop ? 1 : basicBlock.Instructions.Count;
        var begin = basicBlock.IsInfiniteLoop && _printInfiniteLoop ? 1 : 0;
        for (var j = begin; j < count; j++)
        {
            var inst = basicBlock.Instructions[j];
            Indent();
            inst.Accept(this);
            if (inst is not IfStatement && j != basicBlock.Instructions.Count - 1)
            {
                _builder.Append('\n');
            }
        }

        _printInfiniteLoop = false;
    }

    public void VisitExpression(Expression expression)
    {
        
    }

    public void VisitConstant(Constant constant)
    {
        switch (constant.ConstType)
        {
            case Constant.ConstantType.ConstNumber:
                _builder.Append(constant.Number.ToString(CultureInfo.InvariantCulture));
                break;
            case Constant.ConstantType.ConstInteger:
                _builder.Append(constant.Integer);
                break;
            case Constant.ConstantType.ConstString:
                _builder.Append("\"" + constant.String + "\"");
                break;
            case Constant.ConstantType.ConstBool:
                _builder.Append(constant.Boolean ? "true" : "false");
                break;
            case Constant.ConstantType.ConstTable:
                _builder.Append("{}");
                break;
            case Constant.ConstantType.ConstVarargs:
                _builder.Append("...");
                break;
            case Constant.ConstantType.ConstNil:
                _builder.Append("nil");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void VisitClosure(Closure closure)
    {
        closure.Function.Accept(this);
    }

    public void VisitIdentifier(Identifier identifier)
    {
        if (identifier.Type == Identifier.IdentifierType.Varargs)
        {
            _builder.Append("...");
            return;
        }
        _builder.Append(identifier.Name);
    }

    public void VisitIdentifierReference(IdentifierReference identifierReference)
    {
        // Detect a Lua 5.3 global variable and don't display it as a table reference
        var isGlobal = identifierReference.Identifier.Type == Identifier.IdentifierType.GlobalTable;
        if (!isGlobal) 
            identifierReference.Identifier.Accept(this);
        foreach (var idx in identifierReference.TableIndices)
        {
            if (isGlobal && idx is Constant { ConstType: Constant.ConstantType.ConstString } g)
            {
                _builder.Append(g.String);
                isGlobal = false;
            }
            else if (/*DotNotation && */idx is Constant { ConstType: Constant.ConstantType.ConstString } c)
            {
                _builder.Append("." + c.String);
            }
            else
            {
                _builder.Append($@"[{idx}]");
            }
        }
    }

    public void VisitConcat(Concat concat)
    {
        // Pattern match special lua this call
        if (concat.HasParentheses)
        {
            _builder.Append('(');
        }
        for (var i = 0; i < concat.Exprs.Count; i++)
        {
            concat.Exprs[i].Accept(this);
            if (i != concat.Exprs.Count - 1)
            {
                _builder.Append(" .. ");
            }
        }
        if (concat.HasParentheses)
        {
            _builder.Append(')');
        }
    }

    public void VisitInitializerList(InitializerList initializerList)
    {
        _builder.Append('{');

        // Pattern match special lua this call
        for (var i = 0; i < initializerList.Exprs.Count; i++)
        {
            if (initializerList.Assignments != null)
            {
                _builder.Append(initializerList.Assignments[i].String + " = ");
            }
            initializerList.Exprs[i].Accept(this);
            if (i != initializerList.Exprs.Count - 1)
            {
                _builder.Append( ", ");
            }
        }

        _builder.Append('}');
    }

    public void VisitBinOp(BinOp binOp)
    {
        var op = binOp.Operation switch
        {
            BinOp.OperationType.OpAdd => "+",
            BinOp.OperationType.OpDiv => "/",
            BinOp.OperationType.OpFloorDiv => "//",
            BinOp.OperationType.OpMod => "%",
            BinOp.OperationType.OpMul => "*",
            BinOp.OperationType.OpPow => "^",
            BinOp.OperationType.OpSub => "-",
            BinOp.OperationType.OpEqual => "==",
            BinOp.OperationType.OpNotEqual => "~=",
            BinOp.OperationType.OpLessThan => "<",
            BinOp.OperationType.OpLessEqual => "<=",
            BinOp.OperationType.OpGreaterThan => ">",
            BinOp.OperationType.OpGreaterEqual => ">=",
            BinOp.OperationType.OpAnd => "and",
            BinOp.OperationType.OpOr => "or",
            BinOp.OperationType.OpBAnd => "&",
            BinOp.OperationType.OpBOr => "|",
            BinOp.OperationType.OpBXOr => "~",
            BinOp.OperationType.OpShiftRight => ">>",
            BinOp.OperationType.OpShiftLeft => "<<",
            BinOp.OperationType.OpLoopCompare => ">?=",
            _ => ""
        };
        if (binOp.HasParentheses)
        {
            _builder.Append('(');
        }
        binOp.Left.Accept(this);
        _builder.Append($" {op} ");
        binOp.Right.Accept(this);
        if (binOp.HasParentheses)
        {
            _builder.Append(')');
        }
    }

    public void VisitUnaryOp(UnaryOp unaryOp)
    {
        var op = unaryOp.Operation switch
        {
            UnaryOp.OperationType.OpNegate => "-",
            UnaryOp.OperationType.OpNot => "not ",
            UnaryOp.OperationType.OpBNot => "~",
            UnaryOp.OperationType.OpLength => "#",
            _ => ""
        };
        if (unaryOp.HasParentheses)
        {
            _builder.Append('(');
        }
        _builder.Append($@"{op}{unaryOp.Exp}");
        if (unaryOp.HasParentheses)
        {
            _builder.Append(')');
        }
    }

    public void VisitFunctionCall(FunctionCall functionCall)
    {
        // Pattern match special lua this call
        var beginArg = 0;
        if (functionCall.Function is IdentifierReference ir && ir.TableIndices.Count == 1 &&
            ir.TableIndices[0] is Constant { ConstType: Constant.ConstantType.ConstString } c &&
            ir.Identifier.Type != Identifier.IdentifierType.GlobalTable)
        {
            if (functionCall.Args.Count >= 1 && functionCall.Args[0] is IdentifierReference thisIdentifier && 
                thisIdentifier.TableIndices.Count == 0 && thisIdentifier.Identifier == ir.Identifier)
            {
                ir.Identifier.Accept(this);
                _builder.Append($@":{c.String}(");
                beginArg = 1;
            }
            else
            {
                ir.Identifier.Accept(this);
                _builder.Append($@".{c.String}(");
            }
        }
        else if (functionCall.Function is IdentifierReference ir2 && ir2.TableIndices.Count == 2 &&
                 ir2.Identifier.Type == Identifier.IdentifierType.GlobalTable &&
                 ir2.TableIndices[1] is Constant { ConstType: Constant.ConstantType.ConstString } c2 &&
                 ir2.TableIndices[0] is Constant { ConstType: Constant.ConstantType.ConstString } c3 &&
                 functionCall.Args.Count >= 1 && functionCall.Args[0] is IdentifierReference thisIdentifier && 
                 thisIdentifier.TableIndices.Count == 1 && thisIdentifier.Identifier == ir2.Identifier && 
                 ir2.TableIndices[0] is Constant { ConstType: Constant.ConstantType.ConstString } c4 && 
                 c3.String == c4.String)
        {
            _builder.Append($@"{c3.String}:{c2.String}(");
            beginArg = 1;
        }
        else
        {
            functionCall.Function.Accept(this);
            _builder.Append('(');
        }
        for (var i = beginArg; i < functionCall.Args.Count; i++)
        {
            functionCall.Args[i].Accept(this);
            if (i != functionCall.Args.Count - 1)
            {
                _builder.Append(", ");
            }
        }
        _builder.Append(')');
    }

    public void VisitAssignment(Assignment assignment)
    {
        var assignmentOp = assignment.IsGenericForAssignment ? " in " : " = ";
        if (assignment.IsLocalDeclaration)
        {
            _builder.Append("local ");
        }
        if (assignment.Left.Count == 1 && !assignment.Left[0].HasIndex && !assignment.Left[0].DotNotation && 
            assignment.Left[0].Identifier.Type == Identifier.IdentifierType.Global && assignment.Right is Closure c)
        {
            _functionName = assignment.Left[0].Identifier.Name;
            c.Function.Accept(this);
            return;
        }
        if (assignment.Left.Count > 0)
        {
            if (assignment.Left.Count == 1 && assignment.Left[0].HasIndex && assignment.Right is Closure)
            {
                assignment.Left[0].DotNotation = true;
                assignment.Left[0].Accept(this);
                _builder.Append(assignmentOp);
                assignment.Right.Accept(this);
            }
            else
            {
                for (int i = 0; i < assignment.Left.Count; i++)
                {
                    assignment.Left[i].Accept(this);
                    if (i != assignment.Left.Count - 1)
                    {
                        _builder.Append(", ");
                    }
                }
                if (assignment.Right != null)
                {
                    _builder.Append(assignmentOp);
                    assignment.Right.Accept(this);
                }
            }
        }
        else
        {
            assignment.Right.Accept(this);
        }
    }

    public void VisitBreak(Break @break)
    {
        _builder.Append("break");
    }

    public void VisitContinue(Continue @continue)
    {
        _builder.Append("continue");
    }

    public void VisitData(Data data)
    {
        _builder.Append("data");
    }

    public void VisitGenericFor(GenericFor genericFor)
    {
        if (genericFor.Iterator is { } a)
        {
            a.IsLocalDeclaration = false;
            a.IsGenericForAssignment = true;
        }

        _builder.Append(@"for ");
        genericFor.Iterator.Accept(this);
        _builder.Append(" do\n");

        _indentLevel += 1;
        genericFor.Body.Accept(this);
        _indentLevel -= 1;
        _builder.Append('\n');
        Indent();
        _builder.Append("end");
        if (genericFor.Follow != null && genericFor.Follow.Instructions.Count > 0)
        {
            _builder.Append('\n');
            genericFor.Follow.Accept(this);
        }
    }

    public void VisitIfStatement(IfStatement ifStatement)
    {
        if (ifStatement.IsElseIf)
        {
            _builder.Append(@"elseif ");
            ifStatement.Condition.Accept(this); 
            _builder.Append(" then\n");
        }
        else
        {
            _builder.Append(@"if ");
            ifStatement.Condition.Accept(this);
            _builder.Append(" then\n");
        }
        if (ifStatement.True != null)
        {
            _indentLevel++;
            ifStatement.True.Accept(this);
            _indentLevel--;
        }
        if (ifStatement.False != null)
        {
            _builder.Append('\n');
            // Check for elseif
            if (ifStatement.False.Instructions.Count == 1 && 
                ifStatement.False.Instructions.First() is IfStatement { Follow: null } s)
            {
                s.IsElseIf = true;
                ifStatement.False.Accept(this);
            }
            else
            {
                Indent();
                _builder.Append("else\n");
                _indentLevel++;
                ifStatement.False.Accept(this);
                _indentLevel--;
            }
        }
        if (!ifStatement.IsElseIf)
        {
            _builder.Append('\n');
        }
        if (!ifStatement.IsElseIf)
        {
            Indent();
            _builder.Append("end");
        }
        if (ifStatement.Follow != null && ifStatement.Follow.Instructions.Count > 0)
        {
            _builder.Append('\n');
            ifStatement.Follow.Accept(this);
        }
    }

    public void VisitJump(Jump jump)
    {
        if (jump.Conditional)
        {
            _builder.Append(@"if ");
            jump.Condition.Accept(this);
            _builder.Append(" else ");
        }
        if (jump.BlockDest != null)
        {
            _builder.Append("goto ");
            _builder.Append(jump.BlockDest.ToString());
        }
        else
        {
            _builder.Append("goto ");
            jump.Dest.Accept(this);
        }
    }

    public void VisitLabel(Label label)
    {
        _builder.Append($@"{label.LabelName}:");
    }

    public void VisitNumericFor(NumericFor numericFor)
    {
        if (numericFor.Initial is { } a)
        {
            a.IsLocalDeclaration = false;
        }

        _builder.Append($@"for ");
        if (numericFor.Initial != null)
            numericFor.Initial.Accept(this);
        _builder.Append(", ");
        numericFor.Limit.Accept(this);
        _builder.Append(", ");
        numericFor.Increment.Accept(this);
        _builder.Append(" do\n");

        _indentLevel++;
        numericFor.Body.Accept(this);
        _indentLevel--;
        
        _builder.Append('\n');
        Indent();
        _builder.Append("end");
        if (numericFor.Follow != null && numericFor.Follow.Instructions.Count > 0)
        {
            _builder.Append('\n');
            numericFor.Follow.Accept(this);
        }
    }

    public void VisitPhiFunction(PhiFunction phiFunction)
    {
        phiFunction.Left.Accept(this);
        _builder.Append(" = phi(");
        for (var i = 0; i < phiFunction.Right.Count; i++)
        {
            if (phiFunction.Right[i] != null)
            {
                phiFunction.Right[i].Accept(this);
            }
            else
            {
                _builder.Append("undefined");
            }
            if (i != phiFunction.Right.Count - 1)
            {
                _builder.Append(", ");
            }
        }
        _builder.Append(')');
    }

    public void VisitPlaceholderInstruction(PlaceholderInstruction placeholderInstruction)
    {
        _builder.Append(placeholderInstruction.Placeholder);
    }

    public void VisitReturn(Return @return)
    {
        if (@return.IsImplicit)
        {
            return;
        }
        _builder.Append("return ");
        for (var i = 0; i < @return.ReturnExpressions.Count; i++)
        {
            @return.ReturnExpressions[i].Accept(this);
            if (i != @return.ReturnExpressions.Count - 1)
            {
                _builder.Append(", ");
            }
        }
    }

    public void VisitWhile(While @while)
    {
        if (@while.IsPostTested)
        {
            _builder.Append(@"repeat\n");
        }
        else
        {
            _builder.Append(@"while ");
            @while.Condition.Accept(this);
            _builder.Append(" do\n");
        }

        _indentLevel++;
        if (@while.IsBlockInlined)
            _printInfiniteLoop = true;
        @while.Body.Accept(this);
        _indentLevel--;
        
        _builder.Append('\n');
        Indent();
        if (@while.IsPostTested)
        {
            _builder.Append($@"until ");
            @while.Condition.Accept(this);
        }
        else
        {
            _builder.Append("end");
        }
        if (@while.Follow != null && @while.Follow.Instructions.Count > 0)
        {
            _builder.Append('\n');
            @while.Follow.Accept(this);
        }
    }
}