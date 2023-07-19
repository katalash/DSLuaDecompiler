using System;
using System.Globalization;
using System.Linq;
using System.Text;
using LuaDecompilerCore.CFG;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore;

public class FunctionPrinter
{
    private StringBuilder _builder = new StringBuilder();
    private int _indentLevel;

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

    private void Append(char value)
    {
        _builder.Append(value);
    }

    private void Append(string value)
    {
        _builder.Append(value);
    }
    
    private void NewLine()
    {
        Append('\n');
    }

    private void PushIndent()
    {
        _indentLevel++;
    }

    private void PopIndent()
    {
        _indentLevel--;
    }

    private void Indent(bool half = false)
    {
        for (var i = 0; i < _indentLevel; i++)
        { 
            if (half && i == _indentLevel - 1)
            {
                Append("  ");
                break;
            }
            Append("    ");
        }
    }

    private void VisitExpression(Expression? expression)
    {
        if (expression == null)
            return;
        switch (expression)
        {
            case EmptyExpression emptyExpression:
                VisitEmptyExpression(emptyExpression);
                break;
            case Constant constant:
                VisitConstant(constant);
                break;
            case Closure closure:
                VisitClosure(closure);
                break;
            case IdentifierReference identifierReference:
                VisitIdentifierReference(identifierReference);
                break;
            case Concat concat:
                VisitConcat(concat);
                break;
            case InitializerList initializerList:
                VisitInitializerList(initializerList);
                break;
            case BinOp binOp:
                VisitBinOp(binOp);
                break;
            case UnaryOp unaryOp:
                VisitUnaryOp(unaryOp);
                break;
            case FunctionCall functionCall:
                VisitFunctionCall(functionCall);
                break;
            default:
                throw new Exception($"Could not match expression {expression}");
        }
    }

    private void VisitInstruction(Instruction instruction)
    {
        switch (instruction)
        {
            case Assignment assignment:
                VisitAssignment(assignment);
                break;
            case Break @break:
                VisitBreak(@break);
                break;
            case Continue @continue:
                VisitContinue(@continue);
                break;
            case Data data:
                VisitData(data);
                break;
            case GenericFor genericFor:
                VisitGenericFor(genericFor);
                break;
            case IfStatement ifStatement:
                VisitIfStatement(ifStatement);
                break;
            case Jump jump:
                VisitJump(jump);
                break;
            case Label label:
                VisitLabel(label);
                break;
            case NumericFor numericFor:
                VisitNumericFor(numericFor);
                break;
            case PhiFunction phiFunction:
                VisitPhiFunction(phiFunction);
                break;
            case PlaceholderInstruction placeholderInstruction:
                VisitPlaceholderInstruction(placeholderInstruction);
                break;
            case Return @return:
                VisitReturn(@return);
                break;
            case While @while:
                VisitWhile(@while);
                break;
            default:
                throw new Exception($"Could not match instruction {instruction}");
        }
    }

    private void VisitFunction(Function function, string? name = null)
    {
        if (function.FunctionId != 0)
        {
            Append(name == null ? @"function (" : $@"function {name}(");
            for (var i = 0; i < function.Parameters.Count; i++)
            {
                VisitIdentifier(function.Parameters[i]);
                if (i != function.Parameters.Count - 1)
                {
                    Append(", ");
                }
            }
            if (function.IsVarargs)
            {
                Append(function.Parameters.Count > 0 ? ", ..." : "...");
            }
            Append(')');
            NewLine();
            _indentLevel += 1;
        }
        if (function.IsAst)
        {
            if (function.InsertDebugComments)
            {
                Indent();
                Append($"-- Function ID = {function.FunctionId}");
                NewLine();
            }
            VisitBasicBlock(function.BeginBlock);
        }
        else
        {
            // Traverse the basic blocks ordered by their ID
            foreach (var b in function.BlockList.OrderBy(a => a.BlockId))
            {
                if (b == function.EndBlock && b != function.BeginBlock)
                {
                    continue;
                }
                Indent(true);
                Append(b.ToStringWithLoop());
                NewLine();
                foreach (var inst in b.PhiFunctions.Values)
                {
                    Indent();
                    VisitPhiFunction(inst);
                    NewLine();
                }
                foreach (var inst in b.Instructions)
                {
                    Indent(inst is Label);
                    VisitInstruction(inst);
                    NewLine();
                }

                // Insert an implicit goto for fallthrough blocks if the destination isn't actually the next block
                var lastInstruction = b.Instructions.Count > 0 ? b.Instructions.Last() : null;
                if (lastInstruction != null && 
                    ((lastInstruction is Jump { Conditional: true } && 
                      b.Successors[0].BlockId != b.BlockId + 1) ||
                     (lastInstruction is not Jump && lastInstruction is not Return && 
                      b.Successors[0].BlockId != (b.BlockId + 1))))
                {
                    for (var i = 0; i < _indentLevel; i++)
                    {
                        Append("    ");
                    }

                    Append("(goto ");
                    Append(b.Successors[0].Name);
                    Append(')');
                    NewLine();
                }
            }
        }
        if (function.FunctionId != 0)
        {
            _indentLevel -= 1;
            for (var i = 0; i < _indentLevel; i++)
            {
                Append("    ");
            }
            Append("end");
        }
    }

    private void VisitBasicBlock(BasicBlock basicBlock, bool printInfiniteLoop = false)
    {
        var count = basicBlock.IsInfiniteLoop && !printInfiniteLoop ? 1 : basicBlock.Instructions.Count;
        var begin = basicBlock.IsInfiniteLoop && printInfiniteLoop ? 1 : 0;
        for (var j = begin; j < count; j++)
        {
            var inst = basicBlock.Instructions[j];
            if (j != begin && (basicBlock.Instructions[j].HasClosure ||
                               basicBlock.Instructions[j - 1].HasClosure))
            {
                // Insert new lines before and after anything with a closure
                NewLine();
            }
            Indent();
            VisitInstruction(inst);
            if (inst is not IfStatement)
            {
                NewLine();
            }
        }
    }

    private void VisitEmptyExpression(EmptyExpression expression)
    {
        
    }

    private void VisitConstant(Constant constant)
    {
        switch (constant.ConstType)
        {
            case Constant.ConstantType.ConstNumber:
                Append(constant.Number.ToString(CultureInfo.InvariantCulture));
                break;
            case Constant.ConstantType.ConstInteger:
                _builder.Append(constant.Integer);
                break;
            case Constant.ConstantType.ConstString:
                Append("\"" + constant.String + "\"");
                break;
            case Constant.ConstantType.ConstBool:
                Append(constant.Boolean ? "true" : "false");
                break;
            case Constant.ConstantType.ConstTable:
                Append("{}");
                break;
            case Constant.ConstantType.ConstVarargs:
                Append("...");
                break;
            case Constant.ConstantType.ConstNil:
                Append("nil");
                break;
            default:
                throw new Exception("Unhandled constant type");
        }
    }

    private void VisitClosure(Closure closure)
    {
        VisitFunction(closure.Function);
    }

    private void VisitIdentifier(Identifier identifier)
    {
        if (identifier.Type == Identifier.IdentifierType.Varargs)
        {
            Append("...");
            return;
        }
        Append(identifier.Name);
    }

    private void VisitIdentifierReference(IdentifierReference identifierReference)
    {
        // Detect a Lua 5.3 global variable and don't display it as a table reference
        var isGlobal = identifierReference.Identifier.Type == Identifier.IdentifierType.GlobalTable;
        if (!isGlobal) 
            VisitIdentifier(identifierReference.Identifier);
        foreach (var idx in identifierReference.TableIndices)
        {
            if (isGlobal && idx is Constant { ConstType: Constant.ConstantType.ConstString } g)
            {
                Append(g.String);
                isGlobal = false;
            }
            else if (/*DotNotation && */idx is Constant { ConstType: Constant.ConstantType.ConstString } c)
            {
                Append("." + c.String);
            }
            else
            {
                Append('[');
                VisitExpression(idx);
                Append(']');
            }
        }
    }

    private void VisitConcat(Concat concat)
    {
        // Pattern match special lua this call
        if (concat.HasParentheses)
        {
            Append('(');
        }
        for (var i = 0; i < concat.Expressions.Count; i++)
        {
            VisitExpression(concat.Expressions[i]);
            if (i != concat.Expressions.Count - 1)
            {
                Append(" .. ");
            }
        }
        if (concat.HasParentheses)
        {
            Append(')');
        }
    }

    private void VisitInitializerList(InitializerList initializerList)
    {
        Append('{');

        // Pattern match special lua this call
        for (var i = 0; i < initializerList.Expressions.Count; i++)
        {
            if (initializerList.Assignments != null)
            {
                Append(initializerList.Assignments[i].String + " = ");
            }
            VisitExpression(initializerList.Expressions[i]);
            if (i != initializerList.Expressions.Count - 1)
            {
                Append( ", ");
            }
        }

        Append('}');
    }

    private void VisitBinOp(BinOp binOp)
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
            BinOp.OperationType.OpBxOr => "~",
            BinOp.OperationType.OpShiftRight => ">>",
            BinOp.OperationType.OpShiftLeft => "<<",
            BinOp.OperationType.OpLoopCompare => ">?=",
            _ => ""
        };
        if (binOp.HasParentheses)
        {
            Append('(');
        }
        VisitExpression(binOp.Left);
        Append($" {op} ");
        VisitExpression(binOp.Right);
        if (binOp.HasParentheses)
        {
            Append(')');
        }
    }

    private void VisitUnaryOp(UnaryOp unaryOp)
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
            Append('(');
        }
        Append($@"{op}");
        VisitExpression(unaryOp.Expression);
        if (unaryOp.HasParentheses)
        {
            Append(')');
        }
    }

    private void VisitFunctionCall(FunctionCall functionCall)
    {
        // Pattern match special lua this call
        var beginArg = 0;
        if (functionCall.Function is IdentifierReference ir && ir.TableIndices.Count == 1 &&
            ir.TableIndices[0] is Constant { ConstType: Constant.ConstantType.ConstString } c &&
            ir.Identifier.Type != Identifier.IdentifierType.GlobalTable)
        {
            VisitIdentifier(ir.Identifier);
            if (functionCall.Args.Count >= 1 && functionCall.Args[0] is IdentifierReference thisIdentifier && 
                thisIdentifier.TableIndices.Count == 0 && thisIdentifier.Identifier == ir.Identifier)
            {
                Append($@":{c.String}(");
                beginArg = 1;
            }
            else
            {
                Append($@".{c.String}(");
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
            Append($@"{c3.String}:{c2.String}(");
            beginArg = 1;
        }
        else
        {
            VisitExpression(functionCall.Function);
            Append('(');
        }
        for (var i = beginArg; i < functionCall.Args.Count; i++)
        {
            VisitExpression(functionCall.Args[i]);
            if (i != functionCall.Args.Count - 1)
            {
                Append(", ");
            }
        }
        Append(')');
    }

    private void VisitAssignment(Assignment assignment)
    {
        var assignmentOp = assignment.IsGenericForAssignment ? " in " : " = ";
        if (assignment.IsLocalDeclaration)
        {
            Append("local ");
        }
        if (assignment is { IsFunctionDeclaration: true, Right: Closure c })
        {
            VisitFunction(c.Function, assignment.Left.Identifier.Name);
            return;
        }
        //if (assignment.IsSingleAssignment && assignment.Left.HasIndex && assignment.Right is Closure)
        if (assignment is { IsSingleAssignment: true, Left.HasIndex: true, Right: Closure })
        {
            VisitIdentifierReference(assignment.Left);
            Append(assignmentOp);
        }
        else if (assignment.LeftList.Count > 0)
        {
            for (var i = 0; i < assignment.LeftList.Count; i++)
            {
                VisitIdentifierReference(assignment.LeftList[i]);
                if (i != assignment.LeftList.Count - 1)
                {
                    Append(", ");
                }
            }
            if (assignment.Right != null)
            {
                Append(assignmentOp);
            }
        }

        if (assignment.Right != null)
        {
            VisitExpression(assignment.Right);
        }
    }

    private void VisitBreak(Break @break)
    {
        Append("break");
    }

    private void VisitContinue(Continue @continue)
    {
        Append("continue");
    }

    private void VisitData(Data data)
    {
        Append("data");
    }

    private void VisitGenericFor(GenericFor genericFor)
    {
        if (genericFor.Iterator is { } a)
        {
            a.IsLocalDeclaration = false;
            a.IsGenericForAssignment = true;
        }

        Append(@"for ");
        VisitAssignment(genericFor.Iterator);
        Append(" do");
        NewLine();

        PushIndent();
        VisitBasicBlock(genericFor.Body);
        PopIndent();
        Indent();
        Append("end");
        
        if (genericFor.Follow is { HasInstructions: true })
        {
            NewLine();
            VisitBasicBlock(genericFor.Follow);
        }
    }

    private void VisitIfStatement(IfStatement ifStatement)
    {
        Append(ifStatement.IsElseIf ? "elseif " : "if ");
        VisitExpression(ifStatement.Condition);
        Append(" then");
        NewLine();

        if (ifStatement.True != null)
        {
            PushIndent();
            VisitBasicBlock(ifStatement.True);
            PopIndent();
        }
        if (ifStatement.False != null)
        {
            // Check for elseif
            if (ifStatement.False.Instructions.Count == 1 && 
                ifStatement.False.Instructions.First() is IfStatement { Follow: null } s)
            {
                s.IsElseIf = true;
                VisitBasicBlock(ifStatement.False);
            }
            else
            {
                Indent();
                Append("else");
                NewLine();
                PushIndent();
                VisitBasicBlock(ifStatement.False);
                PopIndent();
            }
        }
        if (!ifStatement.IsElseIf)
        {
            Indent();
            Append("end");
            NewLine();
        }
        if (ifStatement.Follow is { HasInstructions: true })
        {
            VisitBasicBlock(ifStatement.Follow);
        }
    }

    private void VisitJump(Jump jump)
    {
        if (jump.Conditional)
        {
            Append(@"if ");
            VisitExpression(jump.Condition);
            Append(" else ");
        }
        if (jump.BlockDest != null)
        {
            Append("goto ");
            Append(jump.BlockDest.Name);
        }
        else
        {
            Append("goto ");
            VisitLabel(jump.Dest);
        }
    }

    private void VisitLabel(Label label)
    {
        Append($@"{label.LabelName}:");
    }

    private void VisitNumericFor(NumericFor numericFor)
    {
        if (numericFor.Initial is { } a)
        {
            a.IsLocalDeclaration = false;
        }

        Append(@"for ");
        if (numericFor.Initial != null)
            VisitAssignment(numericFor.Initial);
        Append(", ");
        VisitExpression(numericFor.Limit);
        Append(", ");
        VisitExpression(numericFor.Increment);
        Append(" do");
        NewLine();

        PushIndent();
        VisitBasicBlock(numericFor.Body);
        PopIndent();
        
        Indent();
        Append("end");
        if (numericFor.Follow is { HasInstructions: true })
        { 
            NewLine();
            VisitBasicBlock(numericFor.Follow);
        }
    }

    private void VisitPhiFunction(PhiFunction phiFunction)
    {
        VisitIdentifier(phiFunction.Left);
        Append(" = phi(");
        for (var i = 0; i < phiFunction.Right.Count; i++)
        {
            if (phiFunction.Right[i] is { } right)
            {
                VisitIdentifier(right);
            }
            else
            {
                Append("undefined");
            }
            if (i != phiFunction.Right.Count - 1)
            {
                Append(", ");
            }
        }
        Append(')');
    }

    private void VisitPlaceholderInstruction(PlaceholderInstruction placeholderInstruction)
    {
        Append(placeholderInstruction.Placeholder);
    }

    private void VisitReturn(Return @return)
    {
        if (@return.IsImplicit)
        {
            return;
        }
        Append("return");
        for (var i = 0; i < @return.ReturnExpressions.Count; i++)
        {
            if (i == 0) Append(' ');
            VisitExpression(@return.ReturnExpressions[i]);
            if (i != @return.ReturnExpressions.Count - 1)
            {
                Append(", ");
            }
        }
    }

    private void VisitWhile(While @while)
    {
        if (@while.IsPostTested)
        {
            Append(@"repeat");
            NewLine();
        }
        else
        {
            Append(@"while ");
            VisitExpression(@while.Condition);
            Append(" do");
            NewLine();
        }

        PushIndent();
        VisitBasicBlock(@while.Body, @while.IsBlockInlined);
        PopIndent();
        
        Indent();
        if (@while.IsPostTested)
        {
            Append(@"until ");
            VisitExpression(@while.Condition);
        }
        else
        {
            Append("end");
        }
        if (@while.Follow is { HasInstructions: true })
        { 
            NewLine();
            VisitBasicBlock(@while.Follow);
        }
    }
}