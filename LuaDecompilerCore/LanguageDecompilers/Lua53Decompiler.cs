using System;
using System.Collections.Generic;
using LuaDecompilerCore.IR;
using LuaDecompilerCore.Passes;
using LuaDecompilerCore.Utilities;
using static LuaDecompilerCore.ILanguageDecompiler;

namespace LuaDecompilerCore.LanguageDecompilers;

public class Lua53Decompiler : ILanguageDecompiler
{
    public enum Lua53Ops
    {
        OpMove = 0,
        OpLoadK = 1,
        OpLoadKX = 2,
        OpLoadBool = 3,
        OpLoadNil = 4,
        OpGetUpVal = 5,
        OpGetTabUp = 6,
        OpGetTable = 7,
        OpSetTabUp = 8,
        OpSetUpVal = 9,
        OpSetTable = 10,
        OpNewTable = 11,
        OpSelf = 12,
        OpAdd = 13,
        OpSub = 14,
        OpMul = 15,
        OpMod = 16,
        OpPow = 17,
        OpDiv = 18,
        OpIDiv = 19,
        OpBAnd = 20,
        OpBOr = 21,
        OpBXOr = 22,
        OpShL = 23,
        OpShR = 24,
        OpUnm = 25,
        OpBNot = 26,
        OpNot = 27,
        OpLen = 28,
        OpConcat = 29,
        OpJmp = 30,
        OpEq = 31,
        OpLt = 32,
        OpLe = 33,
        OpTest = 34,
        OpTestSet = 35,
        OpCall = 36,
        OpTailCall = 37,
        OpReturn = 38,
        OpForLoop = 39,
        OpForPrep = 40,
        OpTForCall = 41,
        OpTForLoop = 42,
        OpSetList = 43,
        OpClosure = 44,
        OpVarArg = 45,
        OpExtraArg = 46,
    }

    private static readonly OpProperties[] OpProperties =
    {
        new OpProperties("MOVE", OpMode.IABC),
        new OpProperties("LOADK", OpMode.IABx),
        new OpProperties("LOADKX", OpMode.IABx),
        new OpProperties("LOADBOOL", OpMode.IABC),
        new OpProperties("LOADNIL", OpMode.IABC),
        new OpProperties("GETUPVAL", OpMode.IABC),
        new OpProperties("GETTABUP", OpMode.IABC),
        new OpProperties("GETTABLE", OpMode.IABC),
        new OpProperties("SETTABUP", OpMode.IABC),
        new OpProperties("SETUPVAL", OpMode.IABC),
        new OpProperties("SETTABLE", OpMode.IABC),
        new OpProperties("NEWTABLE", OpMode.IABC),
        new OpProperties("SELF", OpMode.IABC),
        new OpProperties("ADD", OpMode.IABC),
        new OpProperties("SUB", OpMode.IABC),
        new OpProperties("MUL", OpMode.IABC),
        new OpProperties("MOD", OpMode.IABC),
        new OpProperties("POW", OpMode.IABC),
        new OpProperties("DIV", OpMode.IABC),
        new OpProperties("IDIV", OpMode.IABC),
        new OpProperties("BAND", OpMode.IABC),
        new OpProperties("BOR", OpMode.IABC),
        new OpProperties("BXOR", OpMode.IABC),
        new OpProperties("SHL", OpMode.IABC),
        new OpProperties("SHR", OpMode.IABC),
        new OpProperties("UNM", OpMode.IABC),
        new OpProperties("BNOT", OpMode.IABC),
        new OpProperties("NOT", OpMode.IABC),
        new OpProperties("LEN", OpMode.IABC),
        new OpProperties("CONCAT", OpMode.IABC),
        new OpProperties("JMP", OpMode.IAsBx),
        new OpProperties("EQ", OpMode.IABC),
        new OpProperties("LT", OpMode.IABC),
        new OpProperties("LE", OpMode.IABC),
        new OpProperties("TEST", OpMode.IABC),
        new OpProperties("TESTSET", OpMode.IABC),
        new OpProperties("CALL", OpMode.IABC),
        new OpProperties("TAILCALL", OpMode.IABC),
        new OpProperties("RETURN", OpMode.IABC),
        new OpProperties("FORLOOP", OpMode.IAsBx),
        new OpProperties("FORPREP", OpMode.IAsBx),
        new OpProperties("TFORCALL", OpMode.IABC),
        new OpProperties("TFORLOOP", OpMode.IAsBx),
        new OpProperties("SETLIST", OpMode.IABC),
        new OpProperties("CLOSURE", OpMode.IABx),
        new OpProperties("VARARG", OpMode.IABC),
        new OpProperties("EXTRAARG", OpMode.IAx),
    };

    private static IdentifierReference Register(Function irFunction, uint reg)
    {
        return new IdentifierReference(irFunction.GetRegister(reg));
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

    private static Expression RkIr53(Function irFunction, LuaFile.Function function, uint val)
    {
        if ((val & (1 << 8)) == 0)
        {
            return new IdentifierReference(irFunction.GetRegister(val));
        }

        return ToConstantIr(function.Constants[val & ~(1 << 8)], (int)(val & ~(1 << 8)));
    }

    private static void CheckLocal(Assignment a, LuaFile.Function function, int index)
    {
        a.LocalAssignments = function.LocalsAt(index + 1);
    }

    private static Identifier UpValue53(Function irFunction, LuaFile.Function function, uint upValueId)
    {
        var up = irFunction.GetUpValue(upValueId);
        if (upValueId < function.UpValues.Length && function.UpValues[upValueId].InStack)
        {
            up.StackUpValue = true;
        }

        if (function.UpValueNames.Length > 0 && !up.UpValueResolved)
        {
            up.Name = function.UpValueNames[upValueId].Name ?? throw new Exception("Expected non-empty name");
            up.UpValueResolved = true;
        }

        return up;
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
        
        // Create the global table as the first upValue for the root function
        if (function.FunctionId == 0)
        {
            var globalTable = new Identifier
            {
                Name = "Global",
                Type = Identifier.IdentifierType.GlobalTable,
                IsClosureBound = true
            };
            irFunction.UpValueBindings.Add(globalTable);
        }
        
        // Local variable names if they exist in the debug information
        irFunction.ArgumentNames = function.LocalsAt(0);
    }

    public string? Disassemble(LuaFile.Function function)
    {
        return null;
    }

    public void GenerateIr(LuaFile.Function function, Function irFunction, GlobalSymbolTable globalSymbolTable)
    {
        var br = new BinaryReaderEx(false, function.Bytecode);
        for (var i = 0; i < function.Bytecode.Length; i += 4)
        {
            var instruction = br.ReadUInt32();
            var opcode = instruction & 0x3F;
            //uint a = instruction >> 24;
            var a = (instruction >> 6) & 0xFF;
            //uint b = (instruction >> 15) & 0x1FF;
            var b = (instruction >> 23) & 0x1FF;
            //uint c = (instruction >> 6) & 0x1FF;
            var c = (instruction >> 14) & 0x1FF;
            //uint bx = (instruction >> 6) & 0x3FFFF;
            var bx = (instruction >> 14) & 0x3FFFF;
            var sbx = (int)bx - (((1 << 18) - 1) >> 1);
            var pc = i / 4;
            List<Expression> args;
            var instructions = new List<Instruction>();
            Assignment assignment;
            switch ((Lua53Ops)opcode)
            {
                case Lua53Ops.OpMove:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new IdentifierReference(irFunction.GetRegister(b)));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpLoadK:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        ToConstantIr(function.Constants[bx], (int)bx));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpLoadBool:
                    assignment = new Assignment(irFunction.GetRegister(a), new Constant(b == 1, -1));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    if (c > 0)
                    {
                        instructions.Add(new Jump(irFunction.GetLabel((uint)(i / 4 + 2))));
                    }

                    break;
                case Lua53Ops.OpLoadNil:
                    var nilAssignment = new List<IdentifierReference>();
                    for (var arg = (int)a; arg <= a + b; arg++)
                    {
                        nilAssignment.Add(new IdentifierReference(irFunction.GetRegister((uint)arg)));
                    }

                    assignment = new Assignment(nilAssignment, new Constant(Constant.ConstantType.ConstNil, -1));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpGetUpVal:
                    var up = UpValue53(irFunction, function, b);
                    if (b > irFunction.UpValueBindings.Count)
                    {
                        throw new Exception("Reference to unbound upvalue");
                    }

                    up = irFunction.UpValueBindings[(int)b];
                    assignment = new Assignment(irFunction.GetRegister(a), new IdentifierReference(up));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpGetTabUp:
                    up = UpValue53(irFunction, function, b);
                    if (b > irFunction.UpValueBindings.Count)
                    {
                        throw new Exception("Reference to unbound upvalue");
                    }

                    up = irFunction.UpValueBindings[(int)b];
                    var rkir = RkIr53(irFunction, function, c);
                    if (up.StackUpValue && rkir is Constant c1 && c1.ConstType == Constant.ConstantType.ConstString)
                    {
                        assignment = new Assignment(irFunction.GetRegister(a),
                            new IdentifierReference(globalSymbolTable.GetGlobal(c1.String, -1)));
                    }
                    else
                    {
                        assignment = new Assignment(irFunction.GetRegister(a), new IdentifierReference(up, rkir));
                    }

                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpGetTable:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new IdentifierReference(irFunction.GetRegister(b), RkIr53(irFunction, function, c)));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpSetTabUp:
                    up = UpValue53(irFunction, function, a);
                    if (a > irFunction.UpValueBindings.Count)
                    {
                        throw new Exception("Reference to unbound upvalue");
                    }

                    up = irFunction.UpValueBindings[(int)a];
                    rkir = RkIr53(irFunction, function, b);
                    if (up.StackUpValue && rkir is Constant { ConstType: Constant.ConstantType.ConstString } c2)
                    {
                        instructions.Add(new Assignment(globalSymbolTable.GetGlobal(c2.String, -1),
                            RkIr53(irFunction, function, c)));
                    }
                    else
                    {
                        instructions.Add(new Assignment(new IdentifierReference(up, rkir),
                            RkIr53(irFunction, function, c)));
                    }

                    break;
                case Lua53Ops.OpSetUpVal:
                    var up2 = UpValue53(irFunction, function, b);
                    if (function.UpValueNames.Length > 0 && !up2.UpValueResolved)
                    {
                        up2.Name = function.UpValueNames[b].Name ?? throw new Exception();
                        up2.UpValueResolved = true;
                    }

                    instructions.Add(new Assignment(up2, new IdentifierReference(irFunction.GetRegister(a))));
                    break;
                case Lua53Ops.OpNewTable:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new InitializerList(new List<Expression>()));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpSelf:
                    instructions.Add(new Assignment(irFunction.GetRegister(a + 1),
                        new IdentifierReference(irFunction.GetRegister(b))));
                    instructions.Add(new Assignment(irFunction.GetRegister(a),
                        new IdentifierReference(irFunction.GetRegister(b), RkIr53(irFunction, function, c))));
                    break;
                case Lua53Ops.OpAdd:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new BinOp(RkIr53(irFunction, function, b),
                            RkIr53(irFunction, function, c), BinOp.OperationType.OpAdd));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpSub:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new BinOp(RkIr53(irFunction, function, b),
                            RkIr53(irFunction, function, c), BinOp.OperationType.OpSub));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpMul:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new BinOp(RkIr53(irFunction, function, b),
                            RkIr53(irFunction, function, c), BinOp.OperationType.OpMul));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpDiv:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new BinOp(RkIr53(irFunction, function, b),
                            RkIr53(irFunction, function, c), BinOp.OperationType.OpDiv));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpIDiv:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new BinOp(RkIr53(irFunction, function, b),
                            RkIr53(irFunction, function, c), BinOp.OperationType.OpFloorDiv));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpBAnd:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new BinOp(RkIr53(irFunction, function, b),
                            RkIr53(irFunction, function, c), BinOp.OperationType.OpBAnd));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpBOr:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new BinOp(RkIr53(irFunction, function, b),
                            RkIr53(irFunction, function, c), BinOp.OperationType.OpBOr));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpBXOr:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new BinOp(RkIr53(irFunction, function, b),
                            RkIr53(irFunction, function, c), BinOp.OperationType.OpBxOr));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpShL:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new BinOp(RkIr53(irFunction, function, b),
                            RkIr53(irFunction, function, c), BinOp.OperationType.OpShiftLeft));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpShR:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new BinOp(RkIr53(irFunction, function, b),
                            RkIr53(irFunction, function, c), BinOp.OperationType.OpShiftRight));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpPow:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new BinOp(RkIr53(irFunction, function, b), RkIr53(irFunction, function, c),
                            BinOp.OperationType.OpPow));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpUnm:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new UnaryOp(new IdentifierReference(irFunction.GetRegister(b)),
                            UnaryOp.OperationType.OpNegate));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpBNot:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new UnaryOp(new IdentifierReference(irFunction.GetRegister(b)), UnaryOp.OperationType.OpBNot));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpNot:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new UnaryOp(new IdentifierReference(irFunction.GetRegister(b)), UnaryOp.OperationType.OpNot));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpConcat:
                    args = new List<Expression>();
                    for (var arg = (int)b; arg <= c; arg++)
                    {
                        args.Add(new IdentifierReference(irFunction.GetRegister((uint)arg)));
                    }

                    assignment = new Assignment(irFunction.GetRegister(a), new Concat(args));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpJmp:
                    instructions.Add(new Jump(irFunction.GetLabel((uint)(i / 4 + sbx + 1))));
                    break;
                case Lua53Ops.OpEq:
                    if (a == 0)
                    {
                        instructions.Add(new Jump(irFunction.GetLabel((uint)(i / 4 + 2)),
                            new BinOp(RkIr53(irFunction, function, b), RkIr53(irFunction, function, c),
                                BinOp.OperationType.OpEqual)));
                    }
                    else
                    {
                        instructions.Add(new Jump(irFunction.GetLabel((uint)(i / 4 + 2)),
                            new BinOp(RkIr53(irFunction, function, b), RkIr53(irFunction, function, c),
                                BinOp.OperationType.OpNotEqual)));
                    }

                    break;
                case Lua53Ops.OpLt:
                    if (a == 0)
                    {
                        instructions.Add(new Jump(irFunction.GetLabel((uint)(i / 4 + 2)),
                            new BinOp(RkIr53(irFunction, function, b), RkIr53(irFunction, function, c),
                                BinOp.OperationType.OpLessThan)));
                    }
                    else
                    {
                        instructions.Add(new Jump(irFunction.GetLabel((uint)(i / 4 + 2)),
                            new BinOp(RkIr53(irFunction, function, b), RkIr53(irFunction, function, c),
                                BinOp.OperationType.OpGreaterEqual)));
                    }

                    break;
                case Lua53Ops.OpLe:
                    if (a == 0)
                    {
                        instructions.Add(new Jump(irFunction.GetLabel((uint)(i / 4 + 2)),
                            new BinOp(RkIr53(irFunction, function, b), RkIr53(irFunction, function, c),
                                BinOp.OperationType.OpLessEqual)));
                    }
                    else
                    {
                        instructions.Add(new Jump(irFunction.GetLabel((uint)(i / 4 + 2)),
                            new BinOp(RkIr53(irFunction, function, b), RkIr53(irFunction, function, c),
                                BinOp.OperationType.OpGreaterThan)));
                    }

                    break;
                case Lua53Ops.OpTest:
                    if (c == 0)
                    {
                        instructions.Add(new Jump(irFunction.GetLabel((uint)(i / 4 + 2)), Register(irFunction, a)));
                    }
                    else
                    {
                        instructions.Add(new Jump(irFunction.GetLabel((uint)(i / 4 + 2)),
                            new UnaryOp(Register(irFunction, a), UnaryOp.OperationType.OpNot)));
                    }

                    break;
                case Lua53Ops.OpSetTable:
                    instructions.Add(new Assignment(
                        new IdentifierReference(irFunction.GetRegister(a), RkIr53(irFunction, function, b)),
                        RkIr53(irFunction, function, c)));
                    break;
                case Lua53Ops.OpCall:
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
                        rets.Add(new IdentifierReference(irFunction.GetRegister(a)));
                    }

                    var funcall = new FunctionCall(new IdentifierReference(irFunction.GetRegister(a)), args);
                    funcall.HasAmbiguousArgumentCount = b == 0;
                    funcall.HasAmbiguousReturnCount = c == 0;
                    funcall.BeginArg = a + 1;
                    assignment = new Assignment(rets, funcall);
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpTailCall:
                    args = new List<Expression>();
                    for (var arg = (int)a + 1; arg < a + b; arg++)
                    {
                        args.Add(new IdentifierReference(irFunction.GetRegister((uint)arg)));
                    }

                    instructions.Add(new Return(new FunctionCall(new IdentifierReference(irFunction.GetRegister(a)),
                        args)));
                    break;
                case Lua53Ops.OpReturn:
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
                case Lua53Ops.OpForLoop:
                    instructions.Add(new Assignment(new IdentifierReference(irFunction.GetRegister(a)), new BinOp(
                        new IdentifierReference(irFunction.GetRegister(a)),
                        new IdentifierReference(irFunction.GetRegister(a + 2)), BinOp.OperationType.OpAdd)));
                    var jmp = new Jump(irFunction.GetLabel((uint)(i / 4 + 1 + sbx)), new BinOp(
                        new IdentifierReference(irFunction.GetRegister(a)),
                        new IdentifierReference(irFunction.GetRegister(a + 1)), BinOp.OperationType.OpLoopCompare));
                    var pta = new Assignment(irFunction.GetRegister(a + 3), Register(irFunction, a));
                    pta.PropagateAlways = true;
                    jmp.PostTakenAssignment = pta;
                    instructions.Add(jmp);
                    break;
                case Lua53Ops.OpTForCall:
                    args = new List<Expression>();
                    rets = new List<IdentifierReference>();
                    args.Add(new IdentifierReference(irFunction.GetRegister(a + 1)));
                    args.Add(new IdentifierReference(irFunction.GetRegister(a + 2)));
                    if (c == 0)
                    {
                        rets.Add(new IdentifierReference(irFunction.GetRegister(a + 3)));
                    }
                    else
                    {
                        for (var r = (int)a + 3; r <= a + c + 2; r++)
                        {
                            rets.Add(new IdentifierReference(irFunction.GetRegister((uint)r)));
                        }
                    }

                    var functionCall = new FunctionCall(new IdentifierReference(irFunction.GetRegister(a)), args);
                    functionCall.HasAmbiguousReturnCount = c == 0;
                    assignment = new Assignment(rets, functionCall);
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpTForLoop:
                    var jmp2 = new Jump(irFunction.GetLabel((uint)(i / 4 + 1 + sbx)),
                        new BinOp(Register(irFunction, a + 1), new Constant(Constant.ConstantType.ConstNil, -1),
                            BinOp.OperationType.OpEqual));
                    var pta2 = new Assignment(irFunction.GetRegister(a),
                        new IdentifierReference(irFunction.GetRegister(a + 1)));
                    pta2.PropagateAlways = true;
                    jmp2.PostTakenAssignment = pta2;
                    instructions.Add(jmp2);
                    break;
                case Lua53Ops.OpSetList:
                    for (var j = 1; j <= bx % 32 + 1; j++)
                    {
                        instructions.Add(new Assignment(
                            new IdentifierReference(irFunction.GetRegister(a), new Constant(bx - bx % 32 + j, -1)),
                            new IdentifierReference(irFunction.GetRegister(a + (uint)j))));
                    }

                    break;
                case Lua53Ops.OpClosure:
                    assignment = new Assignment(irFunction.GetRegister(a), new Closure(irFunction.LookupClosure(bx)));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpForPrep:
                    instructions.Add(new Jump(irFunction.GetLabel((uint)(i / 4 + 1 + sbx))));
                    break;
                case Lua53Ops.OpLen:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new UnaryOp(new IdentifierReference(irFunction.GetRegister(b)),
                            UnaryOp.OperationType.OpLength));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case Lua53Ops.OpMod:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new BinOp(Register(irFunction, b), RkIr53(irFunction, function, c),
                            BinOp.OperationType.OpMod));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
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
                            instructions.Add(
                                new PlaceholderInstruction($@"{OpProperties[opcode].OpName} {instruction >> 24}"));
                            break;
                    }

                    throw new Exception($@"Unimplemented opcode {OpProperties[opcode].OpName}");
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
        
        // This needs to be moved to a CFG pass because it seems to break some smash scripts.
        //passManager.AddPass("merge-conditional-assignments", new MergeConditionalAssignmentsPass());
        passManager.AddPass("validate-jump-dest-labels", new ValidateJumpDestinationLabelsPass());

        passManager.AddPass("build-cfg", new BuildControlFlowGraphPass());
        passManager.AddPass("resolve-ambiguous-call-args", new ResolveAmbiguousCallArguments());
        passManager.AddPass("complete-lua51-loops", new CompleteLua51LoopsPass());
        passManager.AddPass("resolve-closure-upvals-53", new ResolveClosureUpValues53Pass());
        passManager.AddPass("ssa-transform", new SsaTransformPass());
        
        passManager.AddPass("eliminate-dead-phi-1", new EliminateDeadAssignmentsPass(true));
        passManager.AddPass("expression-propagation-1", new ExpressionPropagationPass(true));
        passManager.AddPass("detect-list-initializers", new DetectListInitializersPass());
        passManager.AddPass("expression-propagation-2", new ExpressionPropagationPass(false));

        passManager.AddPass("merge-compound-conditionals", new MergeCompoundConditionalsPass());
        passManager.AddPass("detect-loops", new DetectLoopsPass());
        passManager.AddPass("detect-break-continue", new DetectLoopBreakContinuePass());
        passManager.AddPass("detect-two-way-conditionals", new DetectTwoWayConditionalsPass());
        passManager.AddPass("simplify-if-else-follow-chain", new SimplifyIfElseFollowChainPass());
        passManager.AddPass("eliminate-dead-phi-2", new EliminateDeadAssignmentsPass(true));
        passManager.AddPass("expression-propagation-3", new ExpressionPropagationPass(false));
        passManager.AddPass("validate-liveness-no-interference", new ValidateLivenessNoInterferencePass());

        passManager.AddPass("drop-ssa-subscripts", new DropSsaSubscriptsPass());
        passManager.AddPass("detect-local-variables", new DetectLocalVariablesPass());
        // irfun.ArgumentNames = fun.LocalsAt(0);
        passManager.AddPass("rename-local-variables", new RenameVariablesPass());
        passManager.AddPass("parenthesize", new ParenthesizePass());

        passManager.AddPass("build-ast", new AstTransformPass());
    }
}