using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;

namespace luadec
{
    public class LuaFile
    {
        public string ReadLuaString(BinaryReaderEx br)
        {
            ulong length = br.ReadUInt64();
            if (length > 0)
            {
                var ret = br.ReadShiftJIS((int)length-1);
                br.AssertByte(0); // Eat null terminator
                return ret;
            }
            return null;
        }

        public class Header
        {
            byte Version;
            byte Endianess;
            byte IntSize;
            byte LongSize;
            byte InstructionSize;
            byte OpSize;
            byte ASize;
            byte BSize;
            byte CSize;
            byte LuaNumberSize;

            public Header(BinaryReaderEx br)
            {
                br.ReadUInt32(); // Magic
                Version = br.ReadByte();
                Endianess = br.ReadByte();
                IntSize = br.ReadByte();
                LongSize = br.ReadByte();
                InstructionSize = br.ReadByte();
                OpSize = br.ReadByte();
                ASize = br.ReadByte();
                BSize = br.ReadByte();
                CSize = br.ReadByte();
                LuaNumberSize = br.ReadByte();
                br.ReadDouble(); // test number
            }
        }

        public class Constant
        {
            public enum ConstantType
            {
                TypeString,
                TypeNumber,
                TypeNil
            }

            public ConstantType Type;
            public string StringValue;
            public double NumberValue;

            public Constant(LuaFile file, BinaryReaderEx br)
            {
                byte type = br.ReadByte();
                if (type == 3)
                {
                    NumberValue = br.ReadDouble();
                    Type = ConstantType.TypeNumber;
                }
                else if (type == 4)
                {
                    StringValue = file.ReadLuaString(br);
                    Type = ConstantType.TypeString;
                }
                else
                {
                    Type = ConstantType.TypeNil;
                }
            }

            public override string ToString()
            {
                if (Type == ConstantType.TypeString)
                {
                    return StringValue;
                }
                else if (Type == ConstantType.TypeNumber)
                {
                    return NumberValue.ToString();
                }
                return "NULL";
            }
        }

        public class Function
        {
            public string Name;
            public int LineDefined;
            public byte Nups;
            public byte NumParams;
            public byte IsVarArg;
            public byte MaxStackSize;
            public int SizeLineInfo;
            public int LocalVarsCount;
            public int UpValuesCount;
            public Constant[] Constants;
            public Function[] ChildFunctions;
            public byte[] Bytecode;

            public Function(LuaFile file, BinaryReaderEx br)
            {
                Name = file.ReadLuaString(br);
                LineDefined = br.ReadInt32();
                Nups = br.ReadByte();
                NumParams = br.ReadByte();
                IsVarArg = br.ReadByte();
                MaxStackSize = br.ReadByte();
                SizeLineInfo = br.ReadInt32();
                LocalVarsCount = br.ReadInt32();
                UpValuesCount = br.ReadInt32();
                int constantsCount = br.ReadInt32();
                Constants = new Constant[constantsCount];
                for (int i = 0; i < constantsCount; i++)
                {
                    Constants[i] = new Constant(file, br);
                }
                int funcCount = br.ReadInt32();
                ChildFunctions = new Function[funcCount];
                for (int i = 0; i < funcCount; i++)
                {
                    ChildFunctions[i] = new Function(file, br);
                }
                int bytecodeCount = br.ReadInt32();
                Bytecode = br.ReadBytes(bytecodeCount * 4);
            }
        }

        public Header LuaHeader;
        public Function MainFunction;

        public LuaFile(BinaryReaderEx br)
        {
            LuaHeader = new Header(br);
            MainFunction = new Function(this, br);
        }
    }
}
