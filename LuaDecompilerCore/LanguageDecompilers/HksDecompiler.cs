using System;
using System.Collections.Generic;
using LuaDecompilerCore.IR;
using LuaDecompilerCore.Passes;
using LuaDecompilerCore.Utilities;
using static LuaDecompilerCore.ILanguageDecompiler;

namespace LuaDecompilerCore.LanguageDecompilers;

/// <summary>
/// Language decompiler for the HavokScript variants used in the Souls games
/// </summary>
public class HksDecompiler : ILanguageDecompiler
{
    private enum LuaHksOps
    {
        OpGetField = 0,
        OpTest = 1,
        OpCallI = 2,
        OpCallC = 3,
        OpEq = 4,
        OpEqBk = 5,
        OpGetGlobal = 6,
        OpMove = 7,
        OpSelf = 8,
        OpReturn = 9,
        OpGetTableS = 10,
        OpGetTableN = 11,
        OpGetTable = 12,
        OpLoadBool = 13,
        OpTForLoop = 14,
        OpSetField = 15,
        OpSetTableS = 16,
        OpSetTableSBK = 17,
        OpSetTableN = 18,
        OpSetTableNBK = 19,
        OpSetTable = 20,
        OpSetTableBK = 21,
        OpTailCallI = 22,
        OpTailCallC = 23,
        OpTailCallM = 24,
        OpLoadK = 25,
        OpLoadNil = 26,
        OpSetGlobal = 27,
        OpJmp = 28,
        OpCallM = 29,
        OpCall = 30,
        OpIntrinsicIndex = 31,
        OpIntrinsicNewIndex = 32,
        OpIntrinsicSelf = 33,
        OpIntrinsicIndexLiteral = 34,
        OpIntrinsicNewIndexLiteral = 35,
        OpIntrinsicSelfLiteral = 36,
        OpTailCall = 37,
        OpGetUpVal = 38,
        OpSetUpVal = 39,
        OpAdd = 40,
        OpAddBk = 41,
        OpSub = 42,
        OpSubBk = 43,
        OpMul = 44,
        OpMulBk = 45,
        OpDiv = 46,
        OpDivBk = 47,
        OpMod = 48,
        OpModBk = 49,
        OpPow = 50,
        OpPowBk = 51,
        OpNewTable = 52,
        OpUnm = 53,
        OpNot = 54,
        OpLen = 55,
        OpLt = 56,
        OpLtBk = 57,
        OpLe = 58,
        OpLeBk = 59,
        OpConcat = 60,
        OpTestSet = 61,
        OpForPrep = 62,
        OpForLoop = 63,
        OpSetList = 64,
        OpClose = 65,
        OpClosure = 66,
        OpVarArg = 67,
        OpTailCallIR1 = 68,
        OpCallIR1 = 69,
        OpSetUpValR1 = 70,
        OpTestR1 = 71,
        OpNotR1 = 72,
        OpGetFieldR1 = 73,
        OpSetFieldR1 = 74,
        OpNewStruct = 75,
        OpData = 76,
        OpSetSlotN = 77,
        OpSetSlotI = 78,
        OpSetSlot = 79,
        OpSetSlotS = 80,
        OpSetSlotMT = 81,
        OpCheckType = 82,
        OpCheckTypeS = 83,
        OpGetSlot = 84,
        OpGetSlotMT = 85,
        OpSelfSlot = 86,
        OpSelfSlotMT = 87,
        OpGetFieldMM = 88,
        OpCheckTypeD = 89,
        OpGetSlotD = 90,
        OpGetGlobalMem = 91,
    }

    private static readonly OpProperties[] OpProperties =
    {
        new OpProperties("GETFIELD", OpMode.IABC),
        new OpProperties("TEST", OpMode.IABC),
        new OpProperties("CALL_I", OpMode.IABC),
        new OpProperties("CALL_C"),
        new OpProperties("EQ", OpMode.IABC),
        new OpProperties("EQ_BK"),
        new OpProperties("GETGLOBAL"),
        new OpProperties("MOVE", OpMode.IABC),
        new OpProperties("SELF", OpMode.IABC),
        new OpProperties("RETURN", OpMode.IABC),
        new OpProperties("GETTABLE_S", OpMode.IABC),
        new OpProperties("GETTABLE_N"),
        new OpProperties("GETTABLE"),
        new OpProperties("LOADBOOL", OpMode.IABC),
        new OpProperties("TFORLOOP", OpMode.IABC),
        new OpProperties("SETFIELD", OpMode.IABC),
        new OpProperties("SETTABLE_S", OpMode.IABC),
        new OpProperties("SETTABLE_S_BK", OpMode.IABC),
        new OpProperties("SETTABLE_N"),
        new OpProperties("SETTABLE_N_BK"),
        new OpProperties("SETTABLE", OpMode.IABC),
        new OpProperties("SETTABLE_BK"),
        new OpProperties("TAILCALL_I", OpMode.IABC),
        new OpProperties("TAILCALL_C"),
        new OpProperties("TAILCALL_M"),
        new OpProperties("LOADK", OpMode.IABx),
        new OpProperties("LOADNIL", OpMode.IABC),
        new OpProperties("SETGLOBAL", OpMode.IABx),
        new OpProperties("JMP", OpMode.IAsBx),
        new OpProperties("CALL_M"),
        new OpProperties("CALL"),
        new OpProperties("INTRINSIC_INDEX"),
        new OpProperties("INTRINSIC_NEWINDEX"),
        new OpProperties("INTRINSIC_SELF"),
        new OpProperties("INTRINSIC_INDEX_LITERAL"),
        new OpProperties("INTRINSIC_NEWINDEX_LITERAL"),
        new OpProperties("INTRINSIC_SELF_LITERAL"),
        new OpProperties("TAILCALL"),
        new OpProperties("GETUPVAL", OpMode.IABC),
        new OpProperties("SETUPVAL", OpMode.IABC),
        new OpProperties("ADD", OpMode.IABC),
        new OpProperties("ADD_BK", OpMode.IABC),
        new OpProperties("SUB", OpMode.IABC),
        new OpProperties("SUB_BK", OpMode.IABC),
        new OpProperties("MUL", OpMode.IABC),
        new OpProperties("MUL_BK", OpMode.IABC),
        new OpProperties("DIV", OpMode.IABC),
        new OpProperties("DIV_BK", OpMode.IABC),
        new OpProperties("MOD", OpMode.IABC),
        new OpProperties("MOD_BK", OpMode.IABC),
        new OpProperties("POW", OpMode.IABC),
        new OpProperties("POW_BK", OpMode.IABC),
        new OpProperties("NEWTABLE", OpMode.IABC),
        new OpProperties("UNM", OpMode.IABC),
        new OpProperties("NOT", OpMode.IABC),
        new OpProperties("LEN", OpMode.IABC),
        new OpProperties("LT", OpMode.IABC),
        new OpProperties("LT_BK", OpMode.IABC),
        new OpProperties("LE", OpMode.IABC),
        new OpProperties("LE_BK", OpMode.IABC),
        new OpProperties("CONCAT", OpMode.IABC),
        new OpProperties("TESTSET"),
        new OpProperties("FORPREP", OpMode.IAsBx),
        new OpProperties("FORLOOP", OpMode.IAsBx),
        new OpProperties("SETLIST", OpMode.IABC),
        new OpProperties("CLOSE"),
        new OpProperties("CLOSURE", OpMode.IABx),
        new OpProperties("VARARG", OpMode.IABC),
        new OpProperties("TAILCALL_I_R1"),
        new OpProperties("CALL_I_R1", OpMode.IABC),
        new OpProperties("SETUPVAL_R1", OpMode.IABC),
        new OpProperties("TEST_R1", OpMode.IABC),
        new OpProperties("NOT_R1"),
        new OpProperties("GETFIELD_R1", OpMode.IABC),
        new OpProperties("SETFIELD_R1", OpMode.IABC),
        new OpProperties("NEWSTRUCT"),
        new OpProperties("DATA", OpMode.IABx),
        new OpProperties("SETSLOTN"),
        new OpProperties("SETSLOTI"),
        new OpProperties("SETSLOT"),
        new OpProperties("SETSLOTS"),
        new OpProperties("SETSLOTMT"),
        new OpProperties("CHECKTYPE"),
        new OpProperties("CHECKTYPES"),
        new OpProperties("GETSLOT"),
        new OpProperties("GETSLOTMT"),
        new OpProperties("SELFSLOT"),
        new OpProperties("SELFSLOTMT"),
        new OpProperties("GETFIELD_MM"),
        new OpProperties("CHECKTYPE_D"),
        new OpProperties("GETSLOT_D"),
        new OpProperties("GETGLOBAL_MEM", OpMode.IABx),
    };

    private static IdentifierReference Register(Function function, uint reg)
    {
        return new IdentifierReference(function.GetRegister(reg));
    }

    private static Constant ToConstantIr(LuaFile.Constant con, int id)
    {
        return con.Type switch
        {
            LuaFile.Constant.ConstantType.TypeNumber => new Constant(con.NumberValue, id),
            LuaFile.Constant.ConstantType.TypeString => new Constant(con.StringValue ?? "", id),
            LuaFile.Constant.ConstantType.TypeBoolean => new Constant(con.BoolValue, id),
            _ => new Constant(Constant.ConstantType.ConstNil, id)
        };
    }

    private static Expression RkIrHks(Function irFunction, LuaFile.Function function, int val, bool sZero)
    {
        if (val >= 0 && !sZero)
        {
            return new IdentifierReference(irFunction.GetRegister((uint)val));
        }

        return sZero ? ToConstantIr(function.Constants[val], val) : ToConstantIr(function.Constants[-val], -val);
    }

    private static void CheckLocal(Assignment a, LuaFile.Function function, int index)
    {
        a.LocalAssignments = function.LocalsAt(index + 1);
    }

    private static void CheckLocal(Data d, LuaFile.Function function, int index)
    {
        d.Locals = function.LocalsAt(index + 1);
    }

    public void InitializeFunction(LuaFile.Function function, Function irFunction)
    {
        var debugCounter = Identifier.GetGlobalTable();
        irFunction.UpValueBindings.Add(debugCounter);

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

    public string? Disassemble(LuaFile.Function function)
    {
        // Not implemented yet
        return null;
    }

    public void GenerateIr(LuaFile.Function function, Function irFunction)
    {
        var br = new BinaryReaderEx(false, function.Bytecode) { BigEndian = true };
        irFunction.BeginBlock.Instructions = new List<Instruction>(function.Bytecode.Length * 6 / 4);
        List<Instruction> instructions = new List<Instruction>(4);
        for (var i = 0; i < function.Bytecode.Length; i += 4)
        {
            var instruction = br.ReadUInt32();
            // Uhhh thanks again hork
            var opcode = (instruction & 0xFF000000) >> 25;
            var a = instruction & 0xFF;
            var c = (int)(instruction & 0x1FF00) >> 8;
            var b = (int)(instruction & 0x1FE0000) >> 17;
            var sZero = false;
            var pc = i / 4;

            if ((b & 0x100) > 0)
            {
                b = -(b & 0xFF);
            }

            if ((c & 0x100) > 0)
            {
                if (c == 0x100)
                {
                    sZero = true;
                }

                c = -(c & 0xFF);
            }

            var bx = (instruction & 0x1FFFF00) >> 8;
            var sbx = (int)bx;
            uint addr;
            List<Expression> args;
            List<IdentifierReference> rets;
            instructions.Clear();
            Assignment assignment;
            switch ((LuaHksOps)opcode)
            {
                case LuaHksOps.OpMove:
                    assignment = new Assignment(irFunction.GetRegister(a), Register(irFunction, (uint)b));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpLoadK:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        ToConstantIr(function.Constants[bx], (int)bx));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpLoadBool:
                    assignment = new Assignment(irFunction.GetRegister(a), new Constant(b == 1, -1));
                    assignment.NilAssignmentReg = a;
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    if (c > 0)
                    {
                        instructions.Add(new JumpLabel(irFunction.GetLabel((uint)(i / 4 + 2))));
                    }

                    break;
                case LuaHksOps.OpLoadNil:
                    var nlist = new List<IdentifierReference>();
                    for (var arg = (int)a; arg <= b; arg++)
                    {
                        nlist.Add(new IdentifierReference(irFunction.GetRegister((uint)arg)));
                    }

                    assignment = new Assignment(nlist, new Constant(Constant.ConstantType.ConstNil, -1));
                    assignment.NilAssignmentReg = a;
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpGetUpVal:
                    var up = irFunction.GetUpValue((uint)b);
                    instructions.Add(new Assignment(irFunction.GetRegister(a), new IdentifierReference(up)));
                    break;
                case LuaHksOps.OpSetUpVal:
                    up = irFunction.GetUpValue((uint)b);
                    if (b > irFunction.UpValueCount || irFunction.UpValueCount == 0)
                    {
                        throw new Exception("Reference to unbound upvalue: " + up);
                    }

                    instructions.Add(new Assignment(up, new IdentifierReference(irFunction.GetRegister(a))));
                    break;
                case LuaHksOps.OpGetGlobalMem:
                case LuaHksOps.OpGetGlobal:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new IdentifierReference(Identifier.GetGlobal(bx)));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpGetTableS:
                case LuaHksOps.OpGetTable:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new IdentifierReference(irFunction.GetRegister((uint)b),
                            RkIrHks(irFunction, function, c, sZero)));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpSetGlobal:
                    instructions.Add(new Assignment(Identifier.GetGlobal(bx),
                        new IdentifierReference(irFunction.GetRegister(a))));
                    break;
                case LuaHksOps.OpNewTable:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new InitializerList(new List<Expression>()));
                    assignment.VarargAssignmentReg = a;
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;

                case LuaHksOps.OpSelf:
                    instructions.Add(new Assignment(
                        irFunction.GetRegister(a + 1), Register(irFunction, (uint)b)));
                    instructions.Add(new Assignment(
                        irFunction.GetRegister(a),
                        new IdentifierReference(irFunction.GetRegister((uint)b),
                            RkIrHks(irFunction, function, c, sZero))));
                    break;
                case LuaHksOps.OpAdd:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(Register(irFunction, (uint)b),
                            RkIrHks(irFunction, function, c, sZero), BinOp.OperationType.OpAdd));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpAddBk:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(ToConstantIr(function.Constants[b], b),
                            Register(irFunction, (uint)c), BinOp.OperationType.OpAdd));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpSub:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(Register(irFunction, (uint)b),
                            RkIrHks(irFunction, function, c, sZero), BinOp.OperationType.OpSub));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpSubBk:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(ToConstantIr(function.Constants[b], b),
                            Register(irFunction, (uint)c), BinOp.OperationType.OpSub));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpMul:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(Register(irFunction, (uint)b),
                            RkIrHks(irFunction, function, c, sZero), BinOp.OperationType.OpMul));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpMulBk:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(ToConstantIr(function.Constants[b], b),
                            Register(irFunction, (uint)c), BinOp.OperationType.OpMul));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpDiv:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(Register(irFunction, (uint)b),
                            RkIrHks(irFunction, function, c, sZero), BinOp.OperationType.OpDiv));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpDivBk:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(ToConstantIr(function.Constants[b], b),
                            Register(irFunction, (uint)c), BinOp.OperationType.OpDiv));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpMod:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(Register(irFunction, (uint)b),
                            RkIrHks(irFunction, function, c, sZero), BinOp.OperationType.OpMod));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpModBk:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(ToConstantIr(function.Constants[b], b),
                            Register(irFunction, (uint)c), BinOp.OperationType.OpMod));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpPow:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(Register(irFunction, (uint)b),
                            RkIrHks(irFunction, function, c, sZero), BinOp.OperationType.OpPow));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpPowBk:
                    assignment = new Assignment(
                        irFunction.GetRegister(a),
                        new BinOp(ToConstantIr(function.Constants[b], b),
                            Register(irFunction, (uint)c), BinOp.OperationType.OpPow));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpUnm:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new UnaryOp(new IdentifierReference(irFunction.GetRegister((uint)b)),
                            UnaryOp.OperationType.OpNegate));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpNot:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new UnaryOp(new IdentifierReference(irFunction.GetRegister((uint)b)),
                            UnaryOp.OperationType.OpNot));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpLen:
                    assignment = new Assignment(irFunction.GetRegister(a),
                        new UnaryOp(new IdentifierReference(irFunction.GetRegister((uint)b)),
                            UnaryOp.OperationType.OpLength));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpConcat:
                    args = new List<Expression>();
                    for (var arg = b; arg <= c; arg++)
                    {
                        args.Add(new IdentifierReference(irFunction.GetRegister((uint)arg)));
                    }

                    assignment = new Assignment(irFunction.GetRegister(a), new Concat(args));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpJmp:
                    addr = (uint)(i / 4 + 2 + ((sbx << 16) >> 16));
                    if ((sbx & 0x10000) != 0)
                    {
                        // Unsigned address?
                        addr = (uint)((sbx & 0xFFFF) + 2 + (uint)(i / 4));
                    }

                    instructions.Add(new JumpLabel(irFunction.GetLabel(addr)));
                    break;
                case LuaHksOps.OpEq:
                    if (a == 0)
                    {
                        instructions.Add(
                            new ConditionalJumpLabel(irFunction.GetLabel((uint)(i / 4 + 2)),
                                new BinOp(Register(irFunction, (uint)b),
                                    RkIrHks(irFunction, function, c, sZero), BinOp.OperationType.OpEqual)));
                    }
                    else
                    {
                        instructions.Add(
                            new ConditionalJumpLabel(irFunction.GetLabel((uint)(i / 4 + 2)),
                                new BinOp(Register(irFunction, (uint)b),
                                    RkIrHks(irFunction, function, c, sZero), BinOp.OperationType.OpNotEqual)));
                    }

                    break;
                case LuaHksOps.OpLt:
                    if (a == 0)
                    {
                        instructions.Add(
                            new ConditionalJumpLabel(irFunction.GetLabel((uint)(i / 4 + 2)),
                                new BinOp(Register(irFunction, (uint)b),
                                    RkIrHks(irFunction, function, c, sZero), BinOp.OperationType.OpLessThan)));
                    }
                    else
                    {
                        instructions.Add(
                            new ConditionalJumpLabel(irFunction.GetLabel((uint)(i / 4 + 2)),
                                new BinOp(Register(irFunction, (uint)b),
                                    RkIrHks(irFunction, function, c, sZero), BinOp.OperationType.OpGreaterEqual)));
                    }

                    break;
                case LuaHksOps.OpLtBk:
                    if (a == 0)
                    {
                        instructions.Add(
                            new ConditionalJumpLabel(irFunction.GetLabel((uint)(i / 4 + 2)),
                                new BinOp(ToConstantIr(function.Constants[b], b),
                                    Register(irFunction, (uint)c), BinOp.OperationType.OpLessThan)));
                    }
                    else
                    {
                        instructions.Add(
                            new ConditionalJumpLabel(irFunction.GetLabel((uint)(i / 4 + 2)),
                                new BinOp(ToConstantIr(function.Constants[b], b),
                                    Register(irFunction, (uint)c), BinOp.OperationType.OpGreaterEqual)));
                    }

                    break;
                case LuaHksOps.OpLe:
                    if (a == 0)
                    {
                        instructions.Add(
                            new ConditionalJumpLabel(irFunction.GetLabel((uint)(i / 4 + 2)),
                                new BinOp(Register(irFunction, (uint)b),
                                    RkIrHks(irFunction, function, c, sZero), BinOp.OperationType.OpLessEqual)));
                    }
                    else
                    {
                        instructions.Add(
                            new ConditionalJumpLabel(irFunction.GetLabel((uint)(i / 4 + 2)),
                                new BinOp(Register(irFunction, (uint)b),
                                    RkIrHks(irFunction, function, c, sZero), BinOp.OperationType.OpGreaterThan)));
                    }

                    break;
                case LuaHksOps.OpLeBk:
                    if (a == 0)
                    {
                        instructions.Add(
                            new ConditionalJumpLabel(irFunction.GetLabel((uint)(i / 4 + 2)),
                                new BinOp(ToConstantIr(function.Constants[b], b),
                                    Register(irFunction, (uint)c), BinOp.OperationType.OpLessEqual)));
                    }
                    else
                    {
                        instructions.Add(new ConditionalJumpLabel(irFunction.GetLabel((uint)(i / 4 + 2)),
                            new BinOp(ToConstantIr(function.Constants[b], b),
                                Register(irFunction, (uint)c), BinOp.OperationType.OpGreaterThan)));
                    }

                    break;
                case LuaHksOps.OpTest:
                case LuaHksOps.OpTestR1:
                    if (c == 0)
                    {
                        instructions.Add(
                            new ConditionalJumpLabel(irFunction.GetLabel((uint)(i / 4 + 2)), Register(irFunction, a)));
                    }
                    else
                    {
                        instructions.Add(
                            new ConditionalJumpLabel(irFunction.GetLabel((uint)(i / 4 + 2)),
                                new UnaryOp(Register(irFunction, a), UnaryOp.OperationType.OpNot)));
                    }

                    break;
                case LuaHksOps.OpSetTableS:
                case LuaHksOps.OpSetTable:
                    instructions.Add(
                        new Assignment(
                            new IdentifierReference(irFunction.GetRegister(a),
                                RkIrHks(irFunction, function, b, false)),
                            RkIrHks(irFunction, function, c, false)));
                    break;
                case LuaHksOps.OpTailCallI:
                case LuaHksOps.OpTailCallIR1:
                    args = new List<Expression>();
                    for (var arg = (int)a + 1; arg < a + b; arg++)
                    {
                        args.Add(new IdentifierReference(irFunction.GetRegister((uint)arg)));
                    }

                    instructions.Add(
                        new Return(new FunctionCall(
                            new IdentifierReference(irFunction.GetRegister(a)), args))
                        {
                            IsTailReturn = true
                        });
                    break;
                case LuaHksOps.OpSetTableSBK:
                    instructions.Add(
                        new Assignment(
                            new IdentifierReference(irFunction.GetRegister(a),
                                ToConstantIr(function.Constants[b], b)),
                            RkIrHks(irFunction, function, c, false)));
                    break;
                case LuaHksOps.OpCallI:
                case LuaHksOps.OpCallIR1:
                case LuaHksOps.OpCall:
                    args = new List<Expression>();
                    rets = new List<IdentifierReference>();
                    for (var arg = (int)a + 1; arg < a + b; arg++)
                    {
                        args.Add(new IdentifierReference(irFunction.GetRegister((uint)arg)));
                    }

                    for (var r = (int)a; r <= a + c - 2; r++)
                    {
                        rets.Add(new IdentifierReference(irFunction.GetRegister((uint)r)));
                    }

                    if (c == 0)
                    {
                        rets.Add(new IdentifierReference(irFunction.GetRegister(a)));
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
                case LuaHksOps.OpReturn:
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
                case LuaHksOps.OpForLoop:
                    addr = (uint)(i / 4 + 2 + ((sbx << 16) >> 16));
                    if ((sbx & 0x10000) != 0)
                    {
                        // Unsigned address?
                        addr = (uint)((sbx & 0xFFFF) + 2 + (uint)(i / 4));
                    }

                    instructions.Add(new Assignment(new IdentifierReference(irFunction.GetRegister(a)),
                        new BinOp(new IdentifierReference(irFunction.GetRegister(a)),
                            new IdentifierReference(irFunction.GetRegister(a + 2)), BinOp.OperationType.OpAdd)));
                    var pta = new Assignment(irFunction.GetRegister(a + 3), Register(irFunction, a));
                    pta.PropagateAlways = true;
                    var jmp = new ConditionalJumpLabel(irFunction.GetLabel(addr), new BinOp(
                        new IdentifierReference(irFunction.GetRegister(a)),
                        new IdentifierReference(irFunction.GetRegister(a + 1)),
                        BinOp.OperationType.OpLoopCompare), pta);
                    instructions.Add(jmp);
                    break;
                case LuaHksOps.OpTForLoop:
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

                    functionCall = new FunctionCall(new IdentifierReference(irFunction.GetRegister(a)), args);
                    functionCall.HasAmbiguousReturnCount = c == 0;
                    assignment = new Assignment(rets, functionCall);
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    instructions.Add(new ConditionalJumpLabel(irFunction.GetLabel((uint)(i / 4 + 2)),
                        new BinOp(Register(irFunction, a + 3),
                            new Constant(Constant.ConstantType.ConstNil, -1), BinOp.OperationType.OpEqual)));
                    instructions.Add(new Assignment(irFunction.GetRegister(a + 2),
                        new IdentifierReference(irFunction.GetRegister(a + 3))));
                    break;
                case LuaHksOps.OpForPrep:
                    addr = (uint)(i / 4 + 2 + ((sbx << 16) >> 16));
                    if ((sbx & 0x10000) != 0)
                    {
                        // Unsigned address?
                        addr = (uint)((sbx & 0xFFFF) + 2 + (uint)(i / 4));
                    }

                    instructions.Add(new JumpLabel(irFunction.GetLabel(addr)));
                    break;
                case LuaHksOps.OpSetList:
                    if (b == 0)
                    {
                        // Ambiguous assignment
                        if (c == 1)
                        {
                            assignment = new Assignment(irFunction.GetRegister(a), Register(irFunction, a + 1));
                            assignment.VarargAssignmentReg = a;
                            assignment.IsAmbiguousVararg = true;
                            CheckLocal(assignment, function, pc);
                            instructions.Add(assignment);
                        }
                    }
                    else
                    {
                        for (var j = 1; j <= b; j++)
                        {
                            assignment = new Assignment(new IdentifierReference(irFunction.GetRegister(a),
                                    new Constant((double)(c - 1) * 32 + j, -1)),
                                new IdentifierReference(irFunction.GetRegister(a + (uint)j)));
                            CheckLocal(assignment, function, pc);
                            instructions.Add(assignment);
                        }
                    }

                    break;
                case LuaHksOps.OpClosure:
                    instructions.Add(new Assignment(irFunction.GetRegister(a),
                        new Closure(irFunction.LookupClosure(bx))));
                    break;
                case LuaHksOps.OpGetField:
                case LuaHksOps.OpGetFieldR1:
                    assignment = new Assignment(Register(irFunction, a),
                        new IdentifierReference(irFunction.GetRegister((uint)b),
                            new Constant(function.Constants[c].ToString(), -1)));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpData:
                    var dat = new Data();
                    CheckLocal(dat, function, pc);
                    instructions.Add(dat);
                    break;
                case LuaHksOps.OpSetField:
                    assignment = new Assignment(new IdentifierReference(irFunction.GetRegister(a),
                            new Constant(function.Constants[b].ToString(), b)),
                        Register(irFunction, (uint)c));
                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    break;
                case LuaHksOps.OpVarArg:
                    var varArgs = new List<IdentifierReference>();
                    for (var arg = (int)a; arg <= a + b - 1; arg++)
                    {
                        varArgs.Add(new IdentifierReference(irFunction.GetRegister((uint)arg)));
                    }

                    if (b != 0)
                    {
                        assignment = new Assignment(varArgs, new IdentifierReference(Identifier.GetVarArgs()));
                    }
                    else
                    {
                        assignment = new Assignment(irFunction.GetRegister(a),
                            new IdentifierReference(Identifier.GetVarArgs()))
                        {
                            IsAmbiguousVararg = true,
                            VarargAssignmentReg = a
                        };
                    }

                    CheckLocal(assignment, function, pc);
                    instructions.Add(assignment);
                    irFunction.IsVarargs = true;
                    break;
                default:
                    switch (OpProperties[opcode].OpMode)
                    {
                        case OpMode.IABC:
                            instructions.Add(
                                new PlaceholderInstruction($@"{OpProperties[opcode].OpName} {a} {b} {c}"));
                            break;
                        case OpMode.IABx:
                            instructions.Add(new PlaceholderInstruction($@"{OpProperties[opcode].OpName} {a} {bx}"));
                            break;
                        case OpMode.IAsBx:
                            instructions.Add(new PlaceholderInstruction(
                                $@"{OpProperties[opcode].OpName} {a} {(sbx & 0x10000) >> 16} {sbx & 0xFFFF}"));
                            break;
                        case OpMode.IAx:
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    throw new Exception($@"Unimplemented opcode {OpProperties[opcode].OpName}");
                    if (OpProperties[opcode].OpName == null)
                    {
                        Console.WriteLine(opcode);
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
        passManager.AddPass("cleanup-havok-instructions", new CleanupHavokInstructionsPass());
        passManager.AddPass("vararg-list-assignment", new RewriteVarargListAssignmentPass());
        passManager.AddPass("merge-multiple-bool-assignment", new MergeMultipleBoolAssignmentPass());
        passManager.AddPass("eliminate-redundant-assignments", new EliminateRedundantAssignmentsPass());
        passManager.AddPass("merge-conditional-jumps", new MergeConditionalJumpsPass());
        passManager.AddPass("validate-jump-dest-labels", new ValidateJumpDestinationLabelsPass());

        passManager.AddPass("build-cfg", new BuildControlFlowGraphPass());
        passManager.AddPass("resolve-ambiguous-call-args", new ResolveAmbiguousCallArguments());
        passManager.AddPass("ssa-transform", new SsaTransformPass());

        passManager.AddPass("eliminate-dead-phi-1", new EliminateDeadAssignmentsPass(true));
        passManager.AddPass("expression-propagation-1", new ExpressionPropagationPass());
        passManager.AddPass("detect-list-initializers", new DetectListInitializersPass());
        passManager.AddPass("expression-propagation-2", new ExpressionPropagationPass());

        passManager.AddPass("merge-compound-conditionals", new MergeCompoundConditionalsPass());
        passManager.AddPass("merge-conditional-assignments", new MergeConditionalAssignmentsPass());
        passManager.AddPass("detect-loops", new DetectLoopsPass());
        passManager.AddPass("detect-break-continue", new DetectLoopBreakContinuePass());
        passManager.AddPass("detect-two-way-conditionals", new DetectTwoWayConditionalsPass());
        passManager.AddPass("simplify-if-else-follow-chain", new SimplifyIfElseFollowChainPass());
        passManager.AddPass("eliminate-dead-phi-2", new EliminateDeadAssignmentsPass(true));
        passManager.AddPass("expression-propagation-3", new ExpressionPropagationPass());
        //passManager.AddPass("validate-liveness-no-interference", new ValidateLivenessNoInterferencePass());

        passManager.AddPass("drop-ssa-subscripts", new DropSsaSubscriptsPass());
        passManager.AddPass("detect-local-variables", new DetectLocalVariablesPass());
        // irfun.ArgumentNames = fun.LocalsAt(0);
        passManager.AddPass("rename-local-variables", new RenameVariablesPass());
        passManager.AddPass("parenthesize", new ParenthesizePass());
        //passManager.AddPass("annotate-env-act", new AnnotateEnvActFunctionsPass());

        passManager.AddPass("build-ast", new AstTransformPass());
    }
}