using System;
using System.Collections.Generic;
using System.Text;
using LuaDecompilerCore.IR;
using LuaDecompilerCore.Passes;
using LuaDecompilerCore.Utilities;
using static LuaDecompilerCore.ILanguageDecompiler;

namespace LuaDecompilerCore.LanguageDecompilers;

/// <summary>
/// Language decompiler for Lua 5.0
/// </summary>
public class Lua50Decompiler : ILanguageDecompiler
{
    private enum Lua50Ops
    {
        OpMove = 0,
        OpLoadK = 1,
        OpLoadBool = 2,
        OpLoadNil = 3,
        OpGetUpVal = 4,
        OpGetGlobal = 5,
        OpGetTable = 6,
        OpSetGlobal = 7,
        OpSetUpVal = 8,
        OpSetTable = 9,
        OpNewTable = 10,
        OpSelf = 11,
        OpAdd = 12,
        OpSub = 13,
        OpMul = 14,
        OpDiv = 15,
        OpPow = 16,
        OpUnm = 17,
        OpNot = 18,
        OpConcat = 19,
        OpJmp = 20,
        OpEq = 21,
        OpLt = 22,
        OpLe = 23,
        OpTest = 24,
        OpCall = 25,
        OpTailCall = 26,
        OpReturn = 27,
        OpForLoop = 28,
        OpTForLoop = 29,
        OpTForPRep = 30,
        OpSetList = 31,
        OpSetListTo = 32,
        OpClose = 33,
        OpClosure = 34
    }

    private static OpProperties[] _opProperties =
    {
        new OpProperties("MOVE", OpMode.IABC),
        new OpProperties("LOADK", OpMode.IABx),
        new OpProperties("LOADBOOL", OpMode.IABC),
        new OpProperties("LOADNIL", OpMode.IABC),
        new OpProperties("GETUPVAL", OpMode.IABC),
        new OpProperties("GETGLOBAL", OpMode.IABx),
        new OpProperties("GETTABLE", OpMode.IABC),
        new OpProperties("SETGLOBAL", OpMode.IABx),
        new OpProperties("SETUPVAL", OpMode.IABC),
        new OpProperties("SETTABLE", OpMode.IABC),
        new OpProperties("NEWTABLE", OpMode.IABC),
        new OpProperties("SELF", OpMode.IABC),
        new OpProperties("ADD", OpMode.IABC),
        new OpProperties("SUB", OpMode.IABC),
        new OpProperties("MUL", OpMode.IABC),
        new OpProperties("DIV", OpMode.IABC),
        new OpProperties("POW", OpMode.IABC),
        new OpProperties("UNM", OpMode.IABC),
        new OpProperties("NOT", OpMode.IABC),
        new OpProperties("CONCAT", OpMode.IABC),
        new OpProperties("JMP", OpMode.IAsBx),
        new OpProperties("EQ", OpMode.IABC),
        new OpProperties("LT", OpMode.IABC),
        new OpProperties("LE", OpMode.IABC),
        new OpProperties("TEST", OpMode.IABC),
        new OpProperties("CALL", OpMode.IABC),
        new OpProperties("TAILCALL", OpMode.IABC),
        new OpProperties("RETURN", OpMode.IABC),
        new OpProperties("FORLOOP", OpMode.IAsBx),
        new OpProperties("TFORLOOP", OpMode.IABC),
        new OpProperties("TFORPREP", OpMode.IAsBx),
        new OpProperties("SETLIST", OpMode.IABx),
        new OpProperties("SETLISTTO", OpMode.IABx),
        new OpProperties("CLOSE", OpMode.IABC),
        new OpProperties("CLOSURE", OpMode.IABx),
    };

    private static string Rk(LuaFile.Function function, uint val)
    {
        return val < 250 ? $@"R({val})" : function.Constants[val - 250].ToString();
    }

    private static Constant ToConstantIr(LuaFile.Constant con, int id)
    {
        return con.Type switch
        {
            LuaFile.Constant.ConstantType.TypeNumber => new Constant(con.NumberValue, id),
            LuaFile.Constant.ConstantType.TypeString => new Constant(con.StringValue ?? "", id),
            LuaFile.Constant.ConstantType.TypeInt => new Constant(con.IntValue, id),
            _ => new Constant(Constant.ConstantType.ConstNil, id)
        };
    }

    private static Expression RkIr(Function irFunction, LuaFile.Function fun, uint val)
    {
        if (val < 250)
        {
            return new IdentifierReference(irFunction.GetRegister(val));
        }

        return ToConstantIr(fun.Constants[val - 250], (int)val - 250);
    }

    private static void CheckLocal(Assignment a, LuaFile.Function function, int index)
    {
        a.LocalAssignments = function.LocalsAt(index + 1);
    }

    public void InitializeFunction(LuaFile.Function function, Function irFunction, GlobalSymbolTable globalSymbolTable)
    {
        // Register closures for all the children
        foreach (var t in function.ChildFunctions)
        {
            var childFunction = new Function(t.FunctionId)
            {
                // UpValue count needs to be set for child functions for analysis to be correct
                UpValueCount = t.NumUpValues
            };
            irFunction.AddClosure(childFunction);
        }
        
        // Local variable names if they exist in the debug information
        irFunction.ArgumentNames = function.LocalsAt(0);
    }

    public string Disassemble(LuaFile.Function function)
    {
        var builder = new StringBuilder();
        builder.AppendLine($@"Constants:");
        for (var i = 0; i < function.Constants.Length; i++)
        {
            builder.AppendLine($@"{i}: {function.Constants[i]}");
        }

        builder.AppendLine();
        var br = new BinaryReaderEx(false, function.Bytecode);
        for (var i = 0; i < function.Bytecode.Length; i += 4)
        {
            var instruction = br.ReadUInt32();
            var opcode = instruction & 0x3F;
            var a = instruction >> 24;
            var b = (instruction >> 15) & 0x1FF;
            var c = (instruction >> 6) & 0x1FF;
            var bx = (instruction >> 6) & 0x3FFFF;
            var sbx = (int)bx - (((1 << 18) - 1) >> 1);
            var args = "";
            switch ((Lua50Ops)opcode)
            {
                case Lua50Ops.OpMove:
                    builder.AppendLine($@"R({a}) := R({b})");
                    break;
                case Lua50Ops.OpLoadK:
                    builder.AppendLine($@"R({a}) := {function.Constants[bx].ToString()}");
                    break;
                case Lua50Ops.OpLoadBool:
                    builder.AppendLine($@"R({a}) := (Bool){b}");
                    builder.AppendLine($@"if ({c}) PC++");
                    break;
                case Lua50Ops.OpGetGlobal:
                    builder.AppendLine($@"R({a}) := Gbl[{function.Constants[bx].ToString()}]");
                    break;
                case Lua50Ops.OpGetTable:
                    builder.AppendLine($@"R({a}) := R({b})[{Rk(function, c)}]");
                    break;
                case Lua50Ops.OpSetGlobal:
                    builder.AppendLine($@"Gbl[{function.Constants[bx].ToString()}] := R({a})");
                    break;
                case Lua50Ops.OpNewTable:
                    builder.AppendLine($@"R({a}) := {{}} size = {b}, {c}");
                    break;
                case Lua50Ops.OpSelf:
                    builder.AppendLine($@"R({a + 1}) := R({b})");
                    builder.AppendLine($@"R({a}) := R({b})[{Rk(function, c)}]");
                    break;
                case Lua50Ops.OpAdd:
                    builder.AppendLine($@"R({a}) := {Rk(function, b)} + {Rk(function, c)}");
                    break;
                case Lua50Ops.OpSub:
                    builder.AppendLine($@"R({a}) := {Rk(function, b)} - {Rk(function, c)}");
                    break;
                case Lua50Ops.OpMul:
                    builder.AppendLine($@"R({a}) := {Rk(function, b)} * {Rk(function, c)}");
                    break;
                case Lua50Ops.OpDiv:
                    builder.AppendLine($@"R({a}) := {Rk(function, b)} / {Rk(function, c)}");
                    break;
                case Lua50Ops.OpPow:
                    builder.AppendLine($@"R({a}) := {Rk(function, b)} ^ {Rk(function, c)}");
                    break;
                case Lua50Ops.OpUnm:
                    builder.AppendLine($@"R({a}) := -R({b})");
                    break;
                case Lua50Ops.OpNot:
                    builder.AppendLine($@"R({a}) := not R({b})");
                    break;
                case Lua50Ops.OpJmp:
                    builder.AppendLine($@"PC += {sbx}");
                    break;
                case Lua50Ops.OpEq:
                    builder.AppendLine($@"if (({Rk(function, b)} == {Rk(function, c)}) ~= {a}) PC++");
                    break;
                case Lua50Ops.OpLt:
                    builder.AppendLine($@"if (({Rk(function, b)} <  {Rk(function, c)}) ~= {a}) PC++");
                    break;
                case Lua50Ops.OpLe:
                    builder.AppendLine($@"if (({Rk(function, b)} <= {Rk(function, c)}) ~= {a}) PC++");
                    break;
                //case Lua502Ops.OpTest:
                //    builder.AppendLine($@"if (R({b}) <=> {c}) then R({a}) := R({b}) else PC++");
                //    break;
                case Lua50Ops.OpSetTable:
                    builder.AppendLine($@"R({a})[{Rk(function, b)}] := R({c})");
                    break;
                case Lua50Ops.OpCall:
                    args = "";
                    for (var arg = (int)a + 1; arg < a + b; arg++)
                    {
                        if (arg != a + 1)
                            args += ", ";
                        args += $@"R({arg})";
                    }

                    builder.AppendLine($@"R({a}) := R({a})({args})");
                    break;
                case Lua50Ops.OpReturn:
                    args = "";
                    for (var arg = (int)a; arg < a + b - 1; arg++)
                    {
                        if (arg != a)
                            args += ", ";
                        args += $@"R({arg})";
                    }

                    builder.AppendLine($@"return {args}");
                    break;
                case Lua50Ops.OpClosure:
                    args = "";
                    for (var arg = (int)a; arg < a + function.ChildFunctions[bx].NumParams; arg++)
                    {
                        args += ", ";
                        args += $@"R({arg})";
                    }

                    builder.AppendLine($@"R({a}) := closure(KPROTO[{bx}]{args})");
                    break;
                default:
                    switch (_opProperties[opcode].OpMode)
                    {
                        case OpMode.IABC:
                            builder.AppendLine(
                                $@"{_opProperties[opcode].OpName} {instruction >> 24} {(instruction >> 15) & 0x1FF} {(instruction >> 6) & 0x1FF}");
                            break;
                        case OpMode.IABx:
                            builder.AppendLine(
                                $@"{_opProperties[opcode].OpName} {instruction >> 24} {(instruction >> 6) & 0x3FFFF}");
                            break;
                        case OpMode.IAsBx:
                            builder.AppendLine(
                                $@"{_opProperties[opcode].OpName} {instruction >> 24} {(instruction >> 6) & 0x3FFFF}");
                            break;
                    }

                    break;
            }
        }

        /*.AppendLine("\nClosures {");
        for (int i = 0; i < function.ChildFunctions.Length; i++)
        {
            builder.AppendLine($@"Closure {i}:");
            DisassembleFunction(function.ChildFunctions[i]);
        }
        builder.AppendLine("}");*/
        return builder.ToString();
    }

    public void GenerateIr(LuaFile.Function function, Function irFunction, GlobalSymbolTable globalSymbolTable)
    {
        var br = new BinaryReaderEx(false, function.Bytecode);
        irFunction.BeginBlock.Instructions = new List<Instruction>(function.Bytecode.Length * 6 / 4);
        List<Instruction> instructions = new List<Instruction>(4);
        for (var i = 0; i < function.Bytecode.Length; i += 4)
        {
            var instruction = br.ReadUInt32();
            var opcode = instruction & 0x3F;
            var a = instruction >> 24;
            var b = (instruction >> 15) & 0x1FF;
            var c = (instruction >> 6) & 0x1FF;
            var bx = (instruction >> 6) & 0x3FFFF;
            var sbx = (int)bx - (((1 << 18) - 1) >> 1);
            var pc = i / 4;
            List<Expression> args;
            instructions.Clear();
            Assignment assignment;

            bool needsUpValueBinding;
            switch ((Lua50Ops)opcode)
            {
                case Lua50Ops.OpMove:
                    assignment = new Assignment(
                        irFunction.GetRegister(a), new IdentifierReference(irFunction.GetRegister(b)));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpLoadK:
                    assignment = new Assignment(irFunction.GetRegister(a), ToConstantIr(function.Constants[bx], (int)bx));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpLoadBool:
                    assignment = new Assignment(irFunction.GetRegister(a), new Constant(b == 1, -1));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    if (c > 0)
                    {
                        instructions.Add(new JumpLabel(irFunction.GetLabel((uint)(i / 4 + 2))));
                    }

                    break;
                case Lua50Ops.OpLoadNil:
                    var nilAssignment = new List<IdentifierReference>();
                    for (var arg = (int)a; arg <= b; arg++)
                    {
                        nilAssignment.Add(new IdentifierReference(irFunction.GetRegister((uint)arg)));
                    }

                    assignment = new Assignment(nilAssignment, new Constant(Constant.ConstantType.ConstNil, -1));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpGetUpVal:
                    var up = irFunction.GetUpValue(b);
                    needsUpValueBinding = false;
                    if (function.UpValueNames.Length > 0 && !up.UpValueResolved)
                    {
                        up.Name = function.UpValueNames[b].Name ?? throw new Exception();
                        up.UpValueResolved = true;
                    }
                    else
                    {
                        if (b >= irFunction.UpValueCount)
                        {
                            throw new Exception("Reference to unbound upvalue");
                        }

                        needsUpValueBinding = true;
                    }

                    assignment = new Assignment(irFunction.GetRegister(a), new IdentifierReference(up));
                    if (needsUpValueBinding)
                        irFunction.GetUpValueInstructions.Add(assignment);
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpGetGlobal:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new IdentifierReference(globalSymbolTable.GetGlobal(function.Constants[bx].ToString(), (int)bx)));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpGetTable:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new IdentifierReference(irFunction.GetRegister(b), RkIr(irFunction, function, c)));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpSetGlobal:
                    instructions.Add(new Assignment(
                        globalSymbolTable.GetGlobal(function.Constants[bx].ToString(), (int)bx),
                        new IdentifierReference(irFunction.GetRegister(a))));
                    break;
                case Lua50Ops.OpSetUpVal:
                    var up2 = irFunction.GetUpValue(b);
                    needsUpValueBinding = false;
                    if (function.UpValueNames.Length > 0 && !up2.UpValueResolved)
                    {
                        up2.Name = function.UpValueNames[b].Name ?? throw new Exception();
                        up2.UpValueResolved = true;
                    }
                    else
                    {
                        if (b >= irFunction.UpValueBindings.Count)
                        {
                            throw new Exception("Reference to unbound upvalue");
                        }

                        needsUpValueBinding = true;
                    }

                    instructions.Add(new Assignment(up2, new IdentifierReference(irFunction.GetRegister(a))));
                    if (needsUpValueBinding)
                        irFunction.SetUpValueInstructions.Add(instructions[^1] as Assignment ?? throw new Exception());
                    break;
                case Lua50Ops.OpNewTable:
                    assignment = new Assignment(
                        irFunction.GetRegister(a), new InitializerList(new List<Expression>()));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpSelf:
                    var op = new Assignment(
                        irFunction.GetRegister(a + 1), new IdentifierReference(irFunction.GetRegister(b)));
                    op.IsSelfAssignment = true;
                    instructions.Add(op);
                    op = new Assignment(
                        irFunction.GetRegister(a),
                        new IdentifierReference(irFunction.GetRegister(b), RkIr(irFunction, function, c)));
                    op.IsSelfAssignment = true;
                    instructions.Add(op);
                    break;
                case Lua50Ops.OpAdd:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(RkIr(irFunction, function, b),
                            RkIr(irFunction, function, c), BinOp.OperationType.OpAdd));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpSub:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(RkIr(irFunction, function, b),
                            RkIr(irFunction, function, c), BinOp.OperationType.OpSub));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpMul:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(RkIr(irFunction, function, b),
                            RkIr(irFunction, function, c), BinOp.OperationType.OpMul));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpDiv:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(RkIr(irFunction, function, b),
                            RkIr(irFunction, function, c), BinOp.OperationType.OpDiv));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpPow:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(RkIr(irFunction, function, b),
                            RkIr(irFunction, function, c), BinOp.OperationType.OpPow));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpUnm:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new UnaryOp(
                            new IdentifierReference(irFunction.GetRegister(b)), UnaryOp.OperationType.OpNegate));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpNot:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new UnaryOp(
                            new IdentifierReference(irFunction.GetRegister(b)), UnaryOp.OperationType.OpNot));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpConcat:
                    args = new List<Expression>();
                    for (var arg = (int)b; arg <= c; arg++)
                    {
                        args.Add(new IdentifierReference(irFunction.GetRegister((uint)arg)));
                    }

                    assignment = new Assignment(irFunction.GetRegister(a), new Concat(args));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpJmp:
                    instructions.Add(new JumpLabel(irFunction.GetLabel((uint)(i / 4 + sbx + 1))));
                    break;
                case Lua50Ops.OpEq:
                    if (a == 0)
                    {
                        instructions.Add(new ConditionalJumpLabel(
                            irFunction.GetLabel((uint)(i / 4 + 2)),
                            new BinOp(RkIr(irFunction, function, b),
                                RkIr(irFunction, function, c), BinOp.OperationType.OpEqual)));
                    }
                    else
                    {
                        instructions.Add(new ConditionalJumpLabel(
                            irFunction.GetLabel((uint)(i / 4 + 2)),
                            new BinOp(RkIr(irFunction, function, b),
                                RkIr(irFunction, function, c), BinOp.OperationType.OpNotEqual)));
                    }

                    break;
                case Lua50Ops.OpLt:
                    if (a == 0)
                    {
                        instructions.Add(new ConditionalJumpLabel(
                            irFunction.GetLabel((uint)(i / 4 + 2)),
                            new BinOp(RkIr(irFunction, function, b),
                                RkIr(irFunction, function, c), BinOp.OperationType.OpLessThan)));
                    }
                    else
                    {
                        instructions.Add(new ConditionalJumpLabel(
                            irFunction.GetLabel((uint)(i / 4 + 2)),
                            new BinOp(RkIr(irFunction, function, b),
                                RkIr(irFunction, function, c), BinOp.OperationType.OpGreaterEqual)));
                    }

                    break;
                case Lua50Ops.OpLe:
                    if (a == 0)
                    {
                        instructions.Add(new ConditionalJumpLabel(
                            irFunction.GetLabel((uint)(i / 4 + 2)),
                            new BinOp(RkIr(irFunction, function, b),
                                RkIr(irFunction, function, c), BinOp.OperationType.OpLessEqual)));
                    }
                    else
                    {
                        instructions.Add(new ConditionalJumpLabel(
                            irFunction.GetLabel((uint)(i / 4 + 2)),
                            new BinOp(RkIr(irFunction, function, b),
                                RkIr(irFunction, function, c), BinOp.OperationType.OpGreaterThan)));
                    }

                    break;
                case Lua50Ops.OpTest:
                    if (c == 0)
                    {
                        instructions.Add(new ConditionalJumpLabel(
                            irFunction.GetLabel((uint)(i / 4 + 2)), RkIr(irFunction, function, b)));
                    }
                    else
                    {
                        instructions.Add(new ConditionalJumpLabel(
                            irFunction.GetLabel((uint)(i / 4 + 2)),
                            new UnaryOp(RkIr(irFunction, function, b), UnaryOp.OperationType.OpNot)));
                    }

                    instructions.Add(new Assignment(
                        irFunction.GetRegister(a), new IdentifierReference(irFunction.GetRegister(b))));
                    break;
                case Lua50Ops.OpSetTable:
                    instructions.Add(new Assignment(
                        new IdentifierReference(irFunction.GetRegister(a), RkIr(irFunction, function, b)),
                        RkIr(irFunction, function, c)));
                    break;
                case Lua50Ops.OpCall:
                    args = new List<Expression>();
                    var rets = new List<IdentifierReference>();
                    for (var arg = (int)a + 1; arg < a + b; arg++)
                    {
                        args.Add(new IdentifierReference(irFunction.GetRegister((uint)arg)));
                    }

                    for (var r = (int)a; r <= (int)a + c - 2; r++)
                    {
                        rets.Add(new IdentifierReference(irFunction.GetRegister((uint)r)));
                    }

                    if (c == 0)
                    {
                        rets.Add(new IdentifierReference(irFunction.GetRegister((uint)a)));
                    }

                    var functionCall = new FunctionCall(new IdentifierReference(irFunction.GetRegister(a)), args)
                    {
                        HasAmbiguousArgumentCount = b == 0,
                        HasAmbiguousReturnCount = c == 0,
                        BeginArg = a + 1
                    };
                    assignment = new Assignment(rets, functionCall);
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpTailCall:
                    args = new List<Expression>();
                    for (var arg = (int)a + 1; arg < a + b; arg++)
                    {
                        args.Add(new IdentifierReference(irFunction.GetRegister((uint)arg)));
                    }

                    var ret2 = new Return(
                        new FunctionCall(new IdentifierReference(irFunction.GetRegister(a)), args));
                    ret2.IsTailReturn = true;
                    instructions.Add(ret2);
                    break;
                case Lua50Ops.OpReturn:
                    args = new List<Expression>();
                    if (b != 0)
                    {
                        for (var arg = (int)a; arg < a + b - 1; arg++)
                        {
                            args.Add(new IdentifierReference(irFunction.GetRegister((uint)arg)));
                        }
                    }

                    var ret = new Return(args);
                    if (b == 0)
                    {
                        ret.BeginRet = a;
                        ret.IsAmbiguousReturnCount = true;
                    }

                    instructions.Add(ret);
                    break;
                case Lua50Ops.OpForLoop:
                    instructions.Add(new Assignment(new IdentifierReference(irFunction.GetRegister(a)),
                        new BinOp(new IdentifierReference(irFunction.GetRegister(a)),
                            new IdentifierReference(irFunction.GetRegister(a + 2)), BinOp.OperationType.OpAdd)));
                    instructions.Add(new ConditionalJumpLabel(irFunction.GetLabel((uint)(i / 4 + 1 + sbx)),
                        new BinOp(new IdentifierReference(irFunction.GetRegister(a)),
                            new IdentifierReference(
                                irFunction.GetRegister(a + 1)), BinOp.OperationType.OpLoopCompare)));
                    break;
                case Lua50Ops.OpSetList:
                case Lua50Ops.OpSetListTo:
                    for (var j = 1; j <= bx % 32 + 1; j++)
                    {
                        var inst = new Assignment(
                            new IdentifierReference(irFunction.GetRegister(a),
                                new Constant((double)(bx - bx % 32 + j), -1)),
                            new IdentifierReference(irFunction.GetRegister(a + (uint)j)))
                        {
                            IsListAssignment = true
                        };
                        instructions.Add(inst);
                    }

                    break;
                case Lua50Ops.OpClosure:
                    assignment = new Assignment(irFunction.GetRegister(a), new Closure(irFunction.LookupClosure(bx)));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                default:
                    switch (_opProperties[opcode].OpMode)
                    {
                        case OpMode.IABC:
                            instructions.Add(new PlaceholderInstruction(
                                $@"{_opProperties[opcode].OpName} {instruction >> 24} {(instruction >> 15) & 0x1FF} {(instruction >> 6) & 0x1FF}"));
                            break;
                        case OpMode.IABx:
                            instructions.Add(new PlaceholderInstruction(
                                $@"{_opProperties[opcode].OpName} {instruction >> 24} {(instruction >> 6) & 0x3FFFF}"));
                            break;
                        case OpMode.IAsBx:
                            instructions.Add(new PlaceholderInstruction(
                                $@"{_opProperties[opcode].OpName} {instruction >> 24} {(instruction >> 6) & 0x3FFFF}"));
                            break;
                        case OpMode.IAx:
                        default:
                            throw new Exception($@"Unimplemented opcode {_opProperties[opcode].OpName}");
                    }

                    break;
            }

            foreach (var inst in instructions)
            {
                inst.OpLocation = i / 4;
                irFunction.BeginBlock.Instructions.Add(inst);
            }
        }
    }

    public void AddDecompilePasses(PassManager passManager)
    {
        passManager.AddPass("apply-labels", new ApplyLabelsPass());
        passManager.AddPass("vararg-list-assignment", new RewriteVarargListAssignmentPass());
        passManager.AddPass("merge-multiple-bool-assignment", new MergeMultipleBoolAssignmentPass());
        passManager.AddPass("eliminate-redundant-assignments", new EliminateRedundantAssignmentsPass());
        passManager.AddPass("merge-conditional-jumps", new MergeConditionalJumpsPass());
        passManager.AddPass("merge-conditional-assignments", new MergeConditionalAssignmentsPass());
        passManager.AddPass("validate-jump-dest-labels", new ValidateJumpDestinationLabelsPass());

        passManager.AddPass("build-cfg", new BuildControlFlowGraphPass());
        passManager.AddPass("resolve-ambiguous-call-args", new ResolveAmbiguousCallArguments());
        passManager.AddPass("ssa-transform", new SsaTransformPass());
        passManager.AddPass("resolve-closure-upvals-50", new ResolveClosureUpValues50Pass());
        passManager.AddPass("eliminate-dead-phi-1", new EliminateDeadAssignmentsPass(true));
        passManager.AddPass("eliminate-unused-phi", new EliminateUnusedPhiFunctionsPass());
        passManager.AddPass("detect-generic-list-initializers-1", new DetectGenericListInitializersPass());
        passManager.AddPass("expression-propagation-1", new ExpressionPropagationPass(true));
        passManager.AddPass("detect-generic-list-initializers-2", new DetectGenericListInitializersPass());
        passManager.AddPass("detect-list-initializers", new DetectListInitializersPass());

        passManager.AddPass("merge-compound-conditionals", new MergeCompoundConditionalsPass());
        passManager.AddPass("detect-loops", new DetectLoopsPass());
        passManager.AddPass("detect-break-continue", new DetectLoopBreakContinuePass());
        passManager.AddPass("detect-two-way-conditionals", new DetectTwoWayConditionalsPass());
        passManager.AddPass("simplify-if-else-follow-chain", new SimplifyIfElseFollowChainPass());
        passManager.AddPass("eliminate-dead-phi-2", new EliminateDeadAssignmentsPass(true));
        passManager.AddPass("validate-liveness-no-interference", new ValidateLivenessNoInterferencePass());

        passManager.AddPass("drop-ssa-subscripts", new DropSsaSubscriptsPass());
        passManager.AddPass("detect-local-variables", new DetectLocalVariablesPass());
        // irfun.ArgumentNames = fun.LocalsAt(0);
        passManager.AddPass("rename-local-variables", new RenameVariablesPass());
        passManager.AddPass("parenthesize", new ParenthesizePass());

        passManager.AddPass("build-ast", new AstTransformPass());
    }
}