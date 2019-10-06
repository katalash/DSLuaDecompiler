using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;

namespace luadec
{
    class LuaDisassembler
    {
        public enum Lua502Ops
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

        public enum OpMode
        {
            IABC,
            IABx,
            IAsBx,
        }

        public class OpProperties
        {
            public string OpName;
            public OpMode OpMode;

            public OpProperties(string name, OpMode mode)
            {
                OpName = name;
                OpMode = mode;
            }
        }

        public static OpProperties[] OpProperties502 =
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

        public static IR.SymbolTable SymbolTable = new IR.SymbolTable();

        private static string RK(LuaFile.Function fun, uint val)
        {
            if (val < 250)
            {
                return $@"R({val})";
            }
            else
            {
                return fun.Constants[val - 250].ToString();
            }
        }

        private static IR.Expression RKIR(LuaFile.Function fun, uint val)
        {
            if (val < 250)
            {
                return new IR.IdentifierReference(SymbolTable.GetRegister(val));
            }
            else
            {
                return ToConstantIR(fun.Constants[val - 250]);
            }
        }

        public static void DisassembleFunction(LuaFile.Function fun)
        {
            Console.WriteLine($@"Constants:");
            for (int i = 0; i < fun.Constants.Length; i++)
            {
                Console.WriteLine($@"{i}: {fun.Constants[i].ToString()}");
            }
            Console.WriteLine();
            BinaryReaderEx br = new BinaryReaderEx(false, fun.Bytecode);
            for (int i = 0; i < fun.Bytecode.Length; i += 4)
            {
                uint instruction = br.ReadUInt32();
                uint opcode = instruction & 0x3F;
                uint a = instruction >> 24;
                uint b = (instruction >> 15) & 0x1FF;
                uint c = (instruction >> 6) & 0x1FF;
                uint bx = (instruction >> 6) & 0x3FFFF;
                int sbx = ((int)bx - (((1 << 18) - 1) >> 1));
                string args = "";
                switch ((Lua502Ops)opcode)
                {
                    case Lua502Ops.OpMove:
                        Console.WriteLine($@"R({a}) := R({b})");
                        break;
                    case Lua502Ops.OpLoadK:
                        Console.WriteLine($@"R({a}) := {fun.Constants[bx].ToString()}");
                        break;
                    case Lua502Ops.OpLoadBool:
                        Console.WriteLine($@"R({a}) := (Bool){b}");
                        Console.WriteLine($@"if ({c}) PC++");
                        break;
                    case Lua502Ops.OpGetGlobal:
                        Console.WriteLine($@"R({a}) := Gbl[{fun.Constants[bx].ToString()}]");
                        break;
                    case Lua502Ops.OpGetTable:
                        Console.WriteLine($@"R({a}) := R({b})[{RK(fun, c)}]");
                        break;
                    case Lua502Ops.OpSetGlobal:
                        Console.WriteLine($@"Gbl[{fun.Constants[bx].ToString()}] := R({a})");
                        break;
                    case Lua502Ops.OpNewTable:
                        Console.WriteLine($@"R({a}) := {{}} size = {b}, {c}");
                        break;
                    case Lua502Ops.OpSelf:
                        Console.WriteLine($@"R({a+1}) := R({b})");
                        Console.WriteLine($@"R({a}) := R({b})[{RK(fun, c)}]");
                        break;
                    case Lua502Ops.OpAdd:
                        Console.WriteLine($@"R({a}) := {RK(fun, b)} + {RK(fun, c)}");
                        break;
                    case Lua502Ops.OpSub:
                        Console.WriteLine($@"R({a}) := {RK(fun, b)} - {RK(fun, c)}");
                        break;
                    case Lua502Ops.OpMul:
                        Console.WriteLine($@"R({a}) := {RK(fun, b)} * {RK(fun, c)}");
                        break;
                    case Lua502Ops.OpDiv:
                        Console.WriteLine($@"R({a}) := {RK(fun, b)} / {RK(fun, c)}");
                        break;
                    case Lua502Ops.OpPow:
                        Console.WriteLine($@"R({a}) := {RK(fun, b)} ^ {RK(fun, c)}");
                        break;
                    case Lua502Ops.OpUnm:
                        Console.WriteLine($@"R({a}) := -R({b})");
                        break;
                    case Lua502Ops.OpNot:
                        Console.WriteLine($@"R({a}) := not R({b})");
                        break;
                    case Lua502Ops.OpJmp:
                        Console.WriteLine($@"PC += {sbx}");
                        break;
                    case Lua502Ops.OpEq:
                        Console.WriteLine($@"if (({RK(fun, b)} == {RK(fun, c)}) ~= {a}) PC++");
                        break;
                    case Lua502Ops.OpLt:
                        Console.WriteLine($@"if (({RK(fun, b)} <  {RK(fun, c)}) ~= {a}) PC++");
                        break;
                    case Lua502Ops.OpLe:
                        Console.WriteLine($@"if (({RK(fun, b)} <= {RK(fun, c)}) ~= {a}) PC++");
                        break;
                    //case Lua502Ops.OpTest:
                    //    Console.WriteLine($@"if (R({b}) <=> {c}) then R({a}) := R({b}) else PC++");
                    //    break;
                    case Lua502Ops.OpSetTable:
                        Console.WriteLine($@"R({a})[{RK(fun, b)}] := R({c})");
                        break;
                    case Lua502Ops.OpCall:
                        args = "";
                        for (int arg = (int)a + 1; arg < a + b; arg++)
                        {
                            if (arg != a + 1)
                                args += ", ";
                            args += $@"R({arg})";
                        }
                        Console.WriteLine($@"R({a}) := R({a})({args})");
                        break;
                    case Lua502Ops.OpReturn:
                        args = "";
                        for (int arg = (int)a; arg < a + b - 1; arg++)
                        {
                            if (arg != a)
                                args += ", ";
                            args += $@"R({arg})";
                        }
                        Console.WriteLine($@"return {args}");
                        break;
                    case Lua502Ops.OpClosure:
                        args = "";
                        for (int arg = (int)a; arg < a + fun.ChildFunctions[bx].NumParams; arg++)
                        {
                            args += ", ";
                            args += $@"R({arg})";
                        }
                        Console.WriteLine($@"R({a}) := closure(KPROTO[{bx}]{args})");
                        break;
                    default:
                        switch (OpProperties502[opcode].OpMode)
                        {
                            case OpMode.IABC:
                                Console.WriteLine($@"{OpProperties502[opcode].OpName} {instruction >> 24} {(instruction >> 15) & 0x1FF} {(instruction >> 6) & 0x1FF}");
                                break;
                            case OpMode.IABx:
                                Console.WriteLine($@"{OpProperties502[opcode].OpName} {instruction >> 24} {(instruction >> 6) & 0x3FFFF}");
                                break;
                            case OpMode.IAsBx:
                                Console.WriteLine($@"{OpProperties502[opcode].OpName} {instruction >> 24} {(instruction >> 6) & 0x3FFFF}");
                                break;
                        }
                        break;
                }
            }

            Console.WriteLine("\nClosures {");
            for (int i = 0; i < fun.ChildFunctions.Length; i++)
            {
                Console.WriteLine($@"Closure {i}:");
                DisassembleFunction(fun.ChildFunctions[i]);
            }
            Console.WriteLine("}");
        }

        private static IR.Constant ToConstantIR(LuaFile.Constant con)
        {
            if (con.Type == LuaFile.Constant.ConstantType.TypeNumber)
            {
                return new IR.Constant(con.NumberValue);
            }
            else if (con.Type == LuaFile.Constant.ConstantType.TypeString)
            {
                return new IR.Constant(con.StringValue);
            }
            return new IR.Constant(IR.Constant.ConstantType.ConstNil);
        }

        public static void GenerateIR(IR.Function irfun, LuaFile.Function fun)
        {
            // First register closures for all the children
            for (int i = 0; i < fun.ChildFunctions.Length; i++)
            {
                irfun.AddClosure(new IR.Function());
            }

            SymbolTable.BeginScope();
            var parameters = new List<IR.Identifier>();
            for (uint i = 0; i < fun.NumParams; i++)
            {
                parameters.Add(SymbolTable.GetRegister(i));
            }
            irfun.SetParameters(parameters);

            BinaryReaderEx br = new BinaryReaderEx(false, fun.Bytecode);
            for (int i = 0; i < fun.Bytecode.Length; i += 4)
            {
                uint instruction = br.ReadUInt32();
                uint opcode = instruction & 0x3F;
                uint a = instruction >> 24;
                uint b = (instruction >> 15) & 0x1FF;
                uint c = (instruction >> 6) & 0x1FF;
                uint bx = (instruction >> 6) & 0x3FFFF;
                int sbx = ((int)bx - (((1 << 18) - 1) >> 1));
                List<IR.Expression> args = null;
                List<IR.IInstruction> instructions = new List<IR.IInstruction>();
                switch ((Lua502Ops)opcode)
                {
                    case Lua502Ops.OpMove:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := R({b})")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.IdentifierReference(SymbolTable.GetRegister(b))));
                        break;
                    case Lua502Ops.OpLoadK:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := {fun.Constants[b].ToString()}")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), ToConstantIR(fun.Constants[bx])));
                        break;
                    case Lua502Ops.OpLoadBool:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := (Bool){b}")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.Constant(b == 1)));
                        //instructions.Add(new IR.PlaceholderInstruction($@"if ({c}) PC++"));
                        if (c > 0)
                        {
                            instructions.Add(new IR.Jump(irfun.GetLabel((uint)((i / 4) + 2))));
                        }
                        break;
                    case Lua502Ops.OpLoadNil:
                        var assn = new List<IR.IdentifierReference>();
                        for (int arg = (int)a; arg <= b; arg++)
                        {
                            assn.Add(new IR.IdentifierReference(SymbolTable.GetRegister((uint)arg)));
                        }
                        instructions.Add(new IR.Assignment(assn, new IR.Constant(IR.Constant.ConstantType.ConstNil)));
                        break;
                    case Lua502Ops.OpGetUpVal:
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.IdentifierReference(SymbolTable.GetUpvalue(b))));
                        break;
                    case Lua502Ops.OpGetGlobal:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := Gbl[{fun.Constants[bx].ToString()}]")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.IdentifierReference(SymbolTable.GetGlobal(fun.Constants[bx].ToString()))));
                        break;
                    case Lua502Ops.OpGetTable:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := R({b})[{RK(fun, c)}]")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.IdentifierReference(SymbolTable.GetRegister(b), RKIR(fun, c))));
                        break;
                    case Lua502Ops.OpSetGlobal:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"Gbl[{fun.Constants[bx].ToString()}] := R({a})")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetGlobal(fun.Constants[bx].ToString()), new IR.IdentifierReference(SymbolTable.GetRegister(a))));
                        break;
                    case Lua502Ops.OpNewTable:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := {{}} size = {b}, {c}")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.Constant(IR.Constant.ConstantType.ConstTable)));
                        break;
                    case Lua502Ops.OpSelf:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a + 1}) := R({b})")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a + 1), new IR.IdentifierReference(SymbolTable.GetRegister(b))));
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := R({b})[{RK(fun, c)}]")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.IdentifierReference(SymbolTable.GetRegister(b), RKIR(fun, c))));
                        break;
                    case Lua502Ops.OpAdd:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := {RK(fun, b)} + {RK(fun, c)}")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.BinOp(RKIR(fun, b), RKIR(fun, c), IR.BinOp.OperationType.OpAdd)));
                        break;
                    case Lua502Ops.OpSub:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := {RK(fun, b)} - {RK(fun, c)}")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.BinOp(RKIR(fun, b), RKIR(fun, c), IR.BinOp.OperationType.OpSub)));
                        break;
                    case Lua502Ops.OpMul:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := {RK(fun, b)} * {RK(fun, c)}")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.BinOp(RKIR(fun, b), RKIR(fun, c), IR.BinOp.OperationType.OpMul)));
                        break;
                    case Lua502Ops.OpDiv:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := {RK(fun, b)} / {RK(fun, c)}")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.BinOp(RKIR(fun, b), RKIR(fun, c), IR.BinOp.OperationType.OpDiv)));
                        break;
                    case Lua502Ops.OpPow:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := {RK(fun, b)} ^ {RK(fun, c)}")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.BinOp(RKIR(fun, b), RKIR(fun, c), IR.BinOp.OperationType.OpPow)));
                        break;
                    case Lua502Ops.OpUnm:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := -R({b})")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a),
                            new IR.UnaryOp(new IR.IdentifierReference(SymbolTable.GetRegister(b)), IR.UnaryOp.OperationType.OpNegate)));
                        break;
                    case Lua502Ops.OpNot:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := not R({b})")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a),
                            new IR.UnaryOp(new IR.IdentifierReference(SymbolTable.GetRegister(b)), IR.UnaryOp.OperationType.OpNot)));
                        break;
                    case Lua502Ops.OpConcat:
                        args = new List<IR.Expression>();
                        for (int arg = (int)b; arg <= c; arg++)
                        {
                            args.Add(new IR.IdentifierReference(SymbolTable.GetRegister((uint)arg)));
                        }
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := R({a})({args})")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.Concat(args)));
                        break;
                    case Lua502Ops.OpJmp:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"PC += {sbx}")));
                        instructions.Add(new IR.Jump(irfun.GetLabel((uint)((i / 4) + sbx + 1))));
                        break;
                    case Lua502Ops.OpEq:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"if (({RK(fun, b)} == {RK(fun, c)}) ~= {a}) PC++")));
                        if (a == 0)
                        {
                            instructions.Add(new IR.Jump(irfun.GetLabel((uint)((i / 4) + 2)), new IR.BinOp(RKIR(fun, b), RKIR(fun, c), IR.BinOp.OperationType.OpEqual)));
                        }
                        else
                        {
                            instructions.Add(new IR.Jump(irfun.GetLabel((uint)((i / 4) + 2)), new IR.BinOp(RKIR(fun, b), RKIR(fun, c), IR.BinOp.OperationType.OpNotEqual)));
                        }
                        break;
                    case Lua502Ops.OpLt:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"if (({RK(fun, b)} < {RK(fun, c)}) ~= {a}) PC++")));
                        if (a == 0)
                        {
                            instructions.Add(new IR.Jump(irfun.GetLabel((uint)((i / 4) + 2)), new IR.BinOp(RKIR(fun, b), RKIR(fun, c), IR.BinOp.OperationType.OpLessThan)));
                        }
                        else
                        {
                            instructions.Add(new IR.Jump(irfun.GetLabel((uint)((i / 4) + 2)), new IR.BinOp(RKIR(fun, b), RKIR(fun, c), IR.BinOp.OperationType.OpGreaterEqual)));
                        }
                        break;
                    case Lua502Ops.OpLe:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"if (({RK(fun, b)} <= {RK(fun, c)}) ~= {a}) PC++")));
                        if (a == 0)
                        {
                            instructions.Add(new IR.Jump(irfun.GetLabel((uint)((i / 4) + 2)), new IR.BinOp(RKIR(fun, b), RKIR(fun, c), IR.BinOp.OperationType.OpLessEqual)));
                        }
                        else
                        {
                            instructions.Add(new IR.Jump(irfun.GetLabel((uint)((i / 4) + 2)), new IR.BinOp(RKIR(fun, b), RKIR(fun, c), IR.BinOp.OperationType.OpGreaterThan)));
                        }
                        break;
                    case Lua502Ops.OpTest:
                        // This op is weird
                        //instructions.Add(new IR.PlaceholderInstruction(($@"if (R({b}) <=> {c}) then R({a}) := R({b}) else PC++")));
                        if (c == 0)
                        {
                            instructions.Add(new IR.Jump(irfun.GetLabel((uint)((i / 4) + 2)), new IR.BinOp(RKIR(fun, b), new IR.Constant(0.0), IR.BinOp.OperationType.OpNotEqual)));
                        }
                        else
                        {
                            instructions.Add(new IR.Jump(irfun.GetLabel((uint)((i / 4) + 2)), new IR.BinOp(RKIR(fun, b), new IR.Constant(0.0), IR.BinOp.OperationType.OpEqual)));
                        }
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.IdentifierReference(SymbolTable.GetRegister(b))));
                        break;
                    case Lua502Ops.OpSetTable:
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a})[{RK(fun, b)}] := R({c})")));
                        instructions.Add(new IR.Assignment(new IR.IdentifierReference(SymbolTable.GetRegister(a), RKIR(fun, b)), RKIR(fun, c)));
                        break;
                    case Lua502Ops.OpCall:
                        args = new List<IR.Expression>();
                        for (int arg = (int)a + 1; arg < a + b; arg++)
                        {
                            args.Add(new IR.IdentifierReference(SymbolTable.GetRegister((uint)arg)));
                        }
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := R({a})({args})")));
                        var funcall = new IR.FunctionCall(new IR.IdentifierReference(SymbolTable.GetRegister(a)), args);
                        funcall.IsIndeterminantArgumentCount = (b == 0);
                        funcall.IsIndeterminantReturnCount = (c == 0);
                        funcall.BeginArg = a + 1;
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), funcall));
                        break;
                    case Lua502Ops.OpTailCall:
                        args = new List<IR.Expression>();
                        for (int arg = (int)a + 1; arg < a + b; arg++)
                        {
                            args.Add(new IR.IdentifierReference(SymbolTable.GetRegister((uint)arg)));
                        }
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := R({a})({args})")));
                        instructions.Add(new IR.Return(new IR.FunctionCall(new IR.IdentifierReference(SymbolTable.GetRegister(a)), args)));
                        break;
                    case Lua502Ops.OpReturn:
                        args = new List<IR.Expression>();
                        if (b != 0)
                        {
                            for (int arg = (int)a; arg < a + b - 1; arg++)
                            {
                                args.Add(new IR.IdentifierReference(SymbolTable.GetRegister((uint)arg)));
                            }
                        }
                        var ret = new IR.Return(args);
                        if (b == 0)
                        {
                            ret.BeginRet = a;
                            ret.IsIndeterminantReturnCount = true;
                        }
                        instructions.Add(ret);
                        //instructions.Add(new IR.PlaceholderInstruction(($@"return {args}")));
                        break;
                    case Lua502Ops.OpForLoop:
                        instructions.Add(new IR.Assignment(new IR.IdentifierReference(SymbolTable.GetRegister(a)), new IR.BinOp(new IR.IdentifierReference(SymbolTable.GetRegister(a)),
                            new IR.IdentifierReference(SymbolTable.GetRegister(a + 2)), IR.BinOp.OperationType.OpAdd)));
                        instructions.Add(new IR.Jump(irfun.GetLabel((uint)((i / 4) + 1 + sbx)), new IR.BinOp(new IR.IdentifierReference(SymbolTable.GetRegister(a)),
                            new IR.IdentifierReference(SymbolTable.GetRegister(a + 1)), IR.BinOp.OperationType.OpLoopCompare)));
                        break;
                    case Lua502Ops.OpSetList:
                    case Lua502Ops.OpSetListTo:
                        for (int j = 1; j <= (bx%32) + 1; j++)
                        {
                            instructions.Add(new IR.Assignment(new IR.IdentifierReference(SymbolTable.GetRegister(a), new IR.Constant((double)(bx - (bx % 32) + j))),
                                new IR.IdentifierReference(SymbolTable.GetRegister(a + (uint)j))));
                        }
                        break;
                    case Lua502Ops.OpClosure:
                        //args = "";
                        //for (int arg = (int)a; arg < a + fun.ChildFunctions[bx].NumParams; arg++)
                        //{
                        //    args += ", ";
                        //    args += $@"R({arg})";
                        //}
                        //instructions.Add(new IR.PlaceholderInstruction(($@"R({a}) := closure(KPROTO[{bx}]{args})")));
                        instructions.Add(new IR.Assignment(SymbolTable.GetRegister(a), new IR.Closure(irfun.LookupClosure(bx))));
                        break;
                    default:
                        switch (OpProperties502[opcode].OpMode)
                        {
                            case OpMode.IABC:
                                instructions.Add(new IR.PlaceholderInstruction(($@"{OpProperties502[opcode].OpName} {instruction >> 24} {(instruction >> 15) & 0x1FF} {(instruction >> 6) & 0x1FF}")));
                                break;
                            case OpMode.IABx:
                                instructions.Add(new IR.PlaceholderInstruction(($@"{OpProperties502[opcode].OpName} {instruction >> 24} {(instruction >> 6) & 0x3FFFF}")));
                                break;
                            case OpMode.IAsBx:
                                instructions.Add(new IR.PlaceholderInstruction(($@"{OpProperties502[opcode].OpName} {instruction >> 24} {(instruction >> 6) & 0x3FFFF}")));
                                break;
                        }
                        //throw new Exception($@"Unimplemented opcode {OpProperties502[opcode].OpName}");
                        break;
                }
                foreach (var inst in instructions)
                {
                    inst.OpLocation = i / 4;
                    irfun.AddInstruction(inst);
                }
            }
            irfun.ApplyLabels();

            // Simple post-ir and idiom recognition analysis passes
            irfun.EliminateRedundantAssignments();
            irfun.MergeConditionalJumps();
            irfun.MergeConditionalAssignments();
            //irfun.PeepholeOptimize();
            irfun.CheckControlFlowIntegrity();

            // Control flow graph construction and SSA conversion
            irfun.ConstructControlFlowGraph();
            irfun.ResolveIndeterminantArguments(SymbolTable);
            irfun.ConvertToSSA(SymbolTable.GetAllRegistersInScope());

            // Data flow passes
            irfun.PerformExpressionPropogation();

            // CFG passes
            irfun.StructureCompoundConditionals();
            irfun.DetectLoops();
            irfun.DetectLoopConditionals();
            irfun.DetectTwoWayConditionals();
            //irfun.StructureCompoundConditionals();
            irfun.EliminateDeadAssignments(true);

            // Convert out of SSA and rename variables
            irfun.DropSSANaive();
            irfun.RenameVariables();

            // Convert to AST
            irfun.ConvertToAST();

            // Now generate IR for all the child closures
            for (int i = 0; i < fun.ChildFunctions.Length; i++)
            {
                GenerateIR(irfun.LookupClosure((uint)i), fun.ChildFunctions[i]);
            }
            SymbolTable.EndScope();
        }
    }
}
