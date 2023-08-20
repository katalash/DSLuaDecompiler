using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    private static readonly OpProperties[] OpProperties =
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
        return val < 250 ? $"R({val})" : function.Constants[val - 250].ToString();
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

    public void InitializeFunction(LuaFile.Function function, Function irFunction)
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
        builder.AppendLine("Constants:");
        for (var i = 0; i < function.Constants.Length; i++)
        {
            builder.AppendLine($"{i}: {function.Constants[i]}");
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

            builder.Append($"{i / 4:D4}: ");
            
            switch (OpProperties[opcode].OpMode)
            {
                case OpMode.IABC:
                    builder.Append(
                        $"{$"{OpProperties[opcode].OpName} {a} {b} {c}",-20}");
                    break;
                case OpMode.IABx:
                    builder.Append(
                        $"{$"{OpProperties[opcode].OpName} {a} {bx}",-20}");
                    break;
                case OpMode.IAsBx:
                    builder.Append(
                        $"{$"{OpProperties[opcode].OpName} {a} {bx}",-20}");
                    break;
            }
            
            switch ((Lua50Ops)opcode)
            {
                case Lua50Ops.OpMove:
                    builder.Append($"-- R({a}) := R({b})");
                    break;
                case Lua50Ops.OpLoadK:
                    builder.Append($"-- R({a}) := {function.Constants[bx].ToString()}");
                    break;
                case Lua50Ops.OpLoadBool:
                    builder.Append($"-- R({a}) := (Bool){b}; ");
                    builder.Append($"if ({c}) PC++ (PC = {i / 4 + 2})");
                    break;
                case Lua50Ops.OpGetGlobal:
                    builder.Append($"-- R({a}) := Gbl[{function.Constants[bx].ToString()}]");
                    break;
                case Lua50Ops.OpGetTable:
                    builder.Append($"-- R({a}) := R({b})[{Rk(function, c)}]");
                    break;
                case Lua50Ops.OpSetGlobal:
                    builder.Append($"-- Gbl[{function.Constants[bx].ToString()}] := R({a})");
                    break;
                case Lua50Ops.OpNewTable:
                    builder.Append($"-- R({a}) := {{}} size = {b}, {c}");
                    break;
                case Lua50Ops.OpSelf:
                    builder.Append($"-- R({a + 1}) := R({b}); ");
                    builder.Append($"R({a}) := R({b})[{Rk(function, c)}]");
                    break;
                case Lua50Ops.OpAdd:
                    builder.Append($"-- R({a}) := {Rk(function, b)} + {Rk(function, c)}");
                    break;
                case Lua50Ops.OpSub:
                    builder.Append($"-- R({a}) := {Rk(function, b)} - {Rk(function, c)}");
                    break;
                case Lua50Ops.OpMul:
                    builder.Append($"-- R({a}) := {Rk(function, b)} * {Rk(function, c)}");
                    break;
                case Lua50Ops.OpDiv:
                    builder.Append($"-- R({a}) := {Rk(function, b)} / {Rk(function, c)}");
                    break;
                case Lua50Ops.OpPow:
                    builder.Append($"-- R({a}) := {Rk(function, b)} ^ {Rk(function, c)}");
                    break;
                case Lua50Ops.OpUnm:
                    builder.Append($"-- R({a}) := -R({b})");
                    break;
                case Lua50Ops.OpNot:
                    builder.Append($"-- R({a}) := not R({b})");
                    break;
                case Lua50Ops.OpJmp:
                    builder.Append($"-- PC += {sbx} (PC = {i / 4 + sbx + 1})");
                    break;
                case Lua50Ops.OpEq:
                    builder.Append($"-- if (({Rk(function, b)} == {Rk(function, c)}) ~= {a}) PC++ (PC = {i / 4 + 2})");
                    break;
                case Lua50Ops.OpLt:
                    builder.Append($"-- if (({Rk(function, b)} <  {Rk(function, c)}) ~= {a}) PC++ (PC = {i / 4 + 2})");
                    break;
                case Lua50Ops.OpLe:
                    builder.Append($"-- if (({Rk(function, b)} <= {Rk(function, c)}) ~= {a}) PC++ (PC = {i / 4 + 2})");
                    break;
                case Lua50Ops.OpTest:
                    builder.Append($"-- if (R({b}) <=> {c}) then R({a}) := R({b}) else PC++ (PC = {i / 4 + 2})");
                    break;
                case Lua50Ops.OpSetTable:
                    builder.Append($"-- R({a})[{Rk(function, b)}] := R({c})");
                    break;
                case Lua50Ops.OpCall:
                    args = "";
                    for (var arg = (int)a + 1; arg < a + b; arg++)
                    {
                        if (arg != a + 1)
                            args += ", ";
                        args += $"R({arg})";
                    }

                    builder.Append($"-- R({a}) := R({a})({args})");
                    break;
                case Lua50Ops.OpReturn:
                    builder.Append("-- return ");
                    for (var arg = (int)a; arg < a + b - 1; arg++)
                    {
                        if (arg != a)
                            builder.Append(", ");
                        builder.Append($"R({arg})");
                    }
                    break;
                case Lua50Ops.OpClosure:
                    builder.Append($"-- R({a}) := closure(KPROTO[{bx}]");
                    for (var arg = (int)a; arg < a + function.ChildFunctions[bx].NumParams; arg++)
                    {
                        builder.Append($", R({arg}");
                    }
                    builder.Append(')');
                    break;
            }

            builder.AppendLine();
        }
        
        return builder.ToString();
    }

    public void GenerateIr(LuaFile.Function function, Function irFunction)
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
                    for (var arg = (int)a; arg <= b; arg++)
                    {
                        // Assignments are unrolled because the Lua compiler will opportunistically fuse them as an
                        // optimization.
                        assignment = new Assignment(new IdentifierReference(irFunction.GetRegister((uint)arg)), 
                            new Constant(Constant.ConstantType.ConstNil, -1));
                        instructions.Add(assignment);
                        
                        // TODO: CheckLocal for fused opcodes
                        // CheckLocal(assignment, function, pc);
                    }
                    break;
                case Lua50Ops.OpGetUpVal:
                    var up = irFunction.GetUpValue(b);
                    if (b >= irFunction.UpValueCount)
                    {
                        throw new Exception("Reference to unbound upvalue");
                    }

                    assignment = new Assignment(irFunction.GetRegister(a), new IdentifierReference(up));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua50Ops.OpGetGlobal:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new IdentifierReference(Identifier.GetGlobal(bx)));
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
                    instructions.Add(new Assignment(Identifier.GetGlobal(bx), 
                        new IdentifierReference(irFunction.GetRegister(a))));
                    break;
                case Lua50Ops.OpSetUpVal:
                    up = irFunction.GetUpValue(b);
                    if (b >= irFunction.UpValueCount)
                    {
                        throw new Exception("Reference to unbound upvalue");
                    }

                    instructions.Add(new Assignment(up, new IdentifierReference(irFunction.GetRegister(a))));
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
                    op.SelfAssignMinRegister = (int)a;
                    instructions.Add(op);
                    var selfIdentifier =
                        new IdentifierReference(irFunction.GetRegister(b), RkIr(irFunction, function, c));
                    selfIdentifier.IsSelfReference = true;
                    op = new Assignment(irFunction.GetRegister(a), selfIdentifier);
                    op.SelfAssignMinRegister = (int)a;
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
                            new IdentifierReference(irFunction.GetRegister(b)), UnaryOp.OperationType.OpNot, true));
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
                                irFunction.GetRegister(a + 1)), BinOp.OperationType.OpLoopCompare), 
                        null, new Interval((int)a, (int)a + 3)));
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
                    var closureFunction = irFunction.LookupClosure(bx);
                    assignment = new Assignment(
                        irFunction.GetRegister(a), 
                        new Closure(closureFunction)) { OpLocation = i / 4 };
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    
                    // Closure instructions will be followed by a move or get upValue instruction for each upValue in
                    // the child closure. Despite the op-code's semantics, the Lua interpreter does not treat the
                    // operation as an assignment but rather a way to simply bind the value in "B" to the closure
                    for (var j = 0; j < closureFunction.UpValueCount; j++)
                    {
                        i += 4;
                        Debug.Assert(i < function.Bytecode.Length);
                        var upValueInstruction = br.ReadUInt32();
                        var upValueOpCode = upValueInstruction & 0x3F;
                        var opB = (upValueInstruction >> 15) & 0x1FF;
                        var upValueIdentifier = upValueOpCode switch
                        {
                            (uint)Lua50Ops.OpMove => Identifier.GetRegister(opB),
                            (uint)Lua50Ops.OpGetUpVal => Identifier.GetUpValue(opB),
                            _ => throw new Exception(
                                "Expected a move or getUpValue instruction for closure upValue binding")
                        };
                        var closureBinding = new ClosureBinding(upValueIdentifier) { OpLocation = i / 4 };
                        instructions.Add(closureBinding);
                    }
                    break;
                default:
                    switch (OpProperties[opcode].OpMode)
                    {
                        case OpMode.IABC:
                            instructions.Add(new PlaceholderInstruction(
                                $@"{OpProperties[opcode].OpName} {instruction >> 24} {(instruction >> 15) & 0x1FF} {(instruction >> 6) & 0x1FF}"));
                            break;
                        case OpMode.IABx:
                            instructions.Add(new PlaceholderInstruction(
                                $@"{OpProperties[opcode].OpName} {instruction >> 24} {(instruction >> 6) & 0x3FFFF}"));
                            break;
                        case OpMode.IAsBx:
                            instructions.Add(new PlaceholderInstruction(
                                $@"{OpProperties[opcode].OpName} {instruction >> 24} {(instruction >> 6) & 0x3FFFF}"));
                            break;
                        case OpMode.IAx:
                        default:
                            throw new Exception($@"Unimplemented opcode {OpProperties[opcode].OpName}");
                    }

                    break;
            }

            foreach (var inst in instructions)
            {
                if (inst.OpLocation == -1)
                    inst.OpLocation = i / 4;
                inst.InstructionIndices = new Interval(irFunction.BeginBlock.Instructions.Count);
                irFunction.BeginBlock.Instructions.Add(inst);
            }
        }
    }

    public void AddDecompilePasses(PassManager passManager)
    {
        passManager.AddPass("apply-labels", new ApplyLabelsPass());
        passManager.AddPass("vararg-list-assignment", new RewriteVarargListAssignmentPass());
        passManager.AddPass("eliminate-redundant-assignments", new EliminateRedundantAssignmentsPass());
        passManager.AddPass("merge-conditional-jumps", new MergeConditionalJumpsPass());
        passManager.AddPass("validate-jump-dest-labels", new ValidateJumpDestinationLabelsPass());

        passManager.AddPass("build-cfg", new BuildControlFlowGraphPass());
        passManager.AddPass("resolve-ambiguous-call-args", new ResolveAmbiguousCallArguments());
        passManager.AddPass("ssa-transform", new SsaTransformPass());
        passManager.AddPass("resolve-closure-up-values-50", new ResolveClosureUpValues50Pass());
        passManager.AddPass("eliminate-dead-phi-1", new EliminateDeadAssignmentsPass(true));
        passManager.AddPass("eliminate-unused-phi", new EliminateUnusedPhiFunctionsPass());
        passManager.AddPass("detect-generic-list-initializers-1", new DetectGenericListInitializersPass());
        
        passManager.PushLoopUntilUnchanged();
        passManager.AddPass("expression-propagation-1", new ExpressionPropagationPass());
        passManager.AddPass("detect-generic-list-initializers-2", new DetectGenericListInitializersPass());
        passManager.AddPass("detect-list-initializers", new DetectListInitializersPass());
        passManager.AddPass("merge-compound-conditionals", new MergeCompoundConditionalsPass());
        passManager.AddPass("merge-conditional-assignments", new MergeConditionalAssignmentsPass());
        passManager.PopLoopUntilUnchanged();

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