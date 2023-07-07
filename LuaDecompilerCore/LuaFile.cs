using System;
using System.Collections.Generic;
using LuaDecompilerCore.Utilities;

namespace LuaDecompilerCore
{
    public class LuaFile
    {
        public enum LuaVersion
        {
            Lua50,
            Lua51HKS,
            Lua53Smash,
        }

        public LuaVersion Version;

        private int _functionCount = 0;

        private int GetNextFunctionId()
        {
            return _functionCount++;
        }

        public static string ReadLuaString(BinaryReaderEx br, LuaVersion version, bool sizeminusone=true)
        {
            ulong length;
            if (version == LuaVersion.Lua53Smash)
            {
                length = br.ReadByte();
                if (!sizeminusone)
                {
                    length++;
                }
            }
            else
            {
                length = br.ReadUInt64();
            }
            if (length > 0)
            {
                string ret;
                if (version == LuaVersion.Lua50)
                {
                    ret = br.ReadShiftJIS((int)length - 1);
                }
                else
                {
                    ret = br.ReadUTF8((int)length - 1);
                }
                if (version != LuaVersion.Lua53Smash)
                {
                    br.AssertByte(0); // Eat null terminator
                }
                return ret;
            }
            return null;
        }

        public class Header
        {
            public LuaVersion LuaVersion;
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
            byte FloatSize;

            public Header(BinaryReaderEx br)
            {
                br.ReadUInt32(); // Magic
                Version = br.ReadByte();
                if (Version == 0x50)
                {
                    LuaVersion = LuaVersion.Lua50;
                }
                else if (Version == 0x51)
                {
                    byte format = br.AssertByte(0x0E); // HKS
                    LuaVersion = LuaVersion.Lua51HKS;
                }
                else if (Version == 0x53)
                {
                    LuaVersion = LuaVersion.Lua53Smash;
                    br.ReadBytes(7); //unk
                }
                if (LuaVersion == LuaVersion.Lua53Smash)
                {
                    Endianess = 0;
                }
                else
                {
                    Endianess = br.ReadByte();
                }
                IntSize = br.ReadByte();
                LongSize = br.ReadByte();
                InstructionSize = br.ReadByte();
                if (LuaVersion == LuaVersion.Lua50)
                {
                    OpSize = br.ReadByte();
                    ASize = br.ReadByte();
                    BSize = br.ReadByte();
                    CSize = br.ReadByte();
                }
                LuaNumberSize = br.ReadByte();
                if (LuaVersion == LuaVersion.Lua53Smash)
                {
                    FloatSize = br.ReadByte();
                }
                if (LuaVersion == LuaVersion.Lua50)
                {
                    br.ReadDouble(); // test number
                }
                else if (LuaVersion == LuaVersion.Lua53Smash)
                {
                    br.ReadUInt64(); // test int
                    br.ReadSingle(); // test number
                }
                else
                {
                    br.ReadByte(); // Isintegral
                }
            }
        }

        public class Constant
        {
            public enum ConstantType
            {
                TypeBoolean,
                TypeString,
                TypeNumber,
                TypeInt,
                TypeNil
            }

            public ConstantType Type;
            public bool BoolValue;
            public string StringValue;
            public double NumberValue;
            public ulong IntValue;

            public Constant(LuaFile file, BinaryReaderEx br, LuaVersion version)
            {
                byte type = br.ReadByte();
                if (type == 1)
                {
                    Type = ConstantType.TypeBoolean;
                    BoolValue = br.ReadBoolean();
                }
                if (type == 3)
                {
                    if (version == LuaVersion.Lua50)
                    {
                        NumberValue = br.ReadDouble();
                    }
                    else
                    {
                        NumberValue = br.ReadSingle();
                    }
                    Type = ConstantType.TypeNumber;
                }
                else if (type == 4 || type == 0x14)
                {
                    StringValue = LuaFile.ReadLuaString(br, version);
                    Type = ConstantType.TypeString;
                }
                else if (type == 0x13)
                {
                    IntValue = br.ReadUInt64();
                    Type = ConstantType.TypeInt;
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

        public class ConstantHKS
        {
            public enum ConstantType
            {
                TypeNil,
                TypeBoolean,
                TypeLightUserdata,
                TypeNumber,
                TypeString,
                TypeTable,
                TypeFunction,
                TypeUserdata,
                TypeThread,
                TypeIFunction,
                TypeCFunction,
                TypeUInt64,
                TypeStuct,
            }

            public ConstantType Type;
            public bool BoolValue;
            public string StringValue;
            public float NumberValue;

            public ConstantHKS(LuaFile file, BinaryReaderEx br)
            {
                byte type = br.ReadByte();
                if (type == 0)
                {
                    Type = ConstantType.TypeNil;
                }
                else if (type == 1)
                {
                    Type = ConstantType.TypeBoolean;
                    BoolValue = br.ReadBoolean();
                }
                else if (type == 3)
                {
                    NumberValue = br.ReadSingle();
                    Type = ConstantType.TypeNumber;
                }
                else if (type == 4)
                {
                    StringValue = LuaFile.ReadLuaString(br, LuaVersion.Lua51HKS);
                    Type = ConstantType.TypeString;
                }
                else
                {
                    throw new Exception("Unimplemented HKS type");
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
                else if (Type == ConstantType.TypeBoolean)
                {
                    return BoolValue ? "true" : "false";
                }
                else if (Type == ConstantType.TypeNil)
                {
                    return "nil";
                }
                return "NULL";
            }
        }

        public class Local
        {
            public string Name;
            public int Start;
            public int End;

            public Local(BinaryReaderEx br, LuaVersion version)
            {
                Name = ReadLuaString(br, version, false);
                Start = br.ReadInt32();
                End = br.ReadInt32();
            }
        }

        /// <summary>
        /// Lua 5.3 upvalue definition
        /// </summary>
        public class Upvalue
        {
            public bool InStack;
            public byte ID;

            public Upvalue(BinaryReaderEx br)
            {
                InStack = br.ReadBoolean();
                ID = br.ReadByte();
            }
        }

        public class UpvalueName
        {
            public string Name;

            public UpvalueName(BinaryReaderEx br, LuaVersion version)
            {
                Name = ReadLuaString(br, version);
            }
        }

        public class Function
        {
            public string Name;
            public string Path;
            public int LineDefined;
            public byte Nups;
            public uint NumParams;
            public uint NumSlots; // HKS
            public byte IsVarArg;
            public byte MaxStackSize;
            public int SizeLineInfo;
            public int LocalVarsCount;
            public int UpValuesCount;
            public Constant[] Constants;
            public ConstantHKS[] ConstantsHKS;
            public Local[] Locals;
            public Dictionary<int, List<Local>> LocalMap;
            public Upvalue[] Upvalues;
            public UpvalueName[] UpvalueNames;
            public Function[] ChildFunctions;
            public byte[] Bytecode;
            public readonly int FunctionID;

            private void ReadLua53Smash(LuaFile file, BinaryReaderEx br)
            {
                Name = LuaFile.ReadLuaString(br, LuaVersion.Lua53Smash);
                LineDefined = br.ReadInt32();
                br.ReadInt32(); // last line
                NumParams = br.ReadByte();
                IsVarArg = br.ReadByte();
                MaxStackSize = br.ReadByte();
                int bytecodeCount = br.ReadInt32();
                Bytecode = br.ReadBytes(bytecodeCount * 4);
                int constantsCount = br.ReadInt32();
                Constants = new Constant[constantsCount];
                for (int i = 0; i < constantsCount; i++)
                {
                    Constants[i] = new Constant(file, br, LuaVersion.Lua53Smash);
                }
                int upvalct = br.ReadInt32();
                Upvalues = new Upvalue[upvalct];
                for (int i = 0; i < upvalct; i++)
                {
                    Upvalues[i] = new Upvalue(br);
                }
                int funcCount = br.ReadInt32();
                ChildFunctions = new Function[funcCount];
                for (int i = 0; i < funcCount; i++)
                {
                    ChildFunctions[i] = new Function(file, LuaVersion.Lua53Smash, br);
                }

                SizeLineInfo = br.ReadInt32();
                // Eat line numbers
                br.ReadInt32s(SizeLineInfo);

                LocalVarsCount = br.ReadInt32();
                Locals = new Local[LocalVarsCount];
                LocalMap = new Dictionary<int, List<Local>>();
                for (int i = 0; i < LocalVarsCount; i++)
                {
                    Locals[i] = new Local(br, LuaVersion.Lua53Smash);
                    if (!Locals[i].Name.StartsWith("("))
                    {
                        if (!LocalMap.ContainsKey(Locals[i].Start))
                        {
                            LocalMap[Locals[i].Start] = new List<Local>();
                        }
                        LocalMap[Locals[i].Start].Add(Locals[i]);
                    }
                }

                //br.ReadUInt32(); // upval names
                UpValuesCount = br.ReadInt32();
                UpvalueNames = new UpvalueName[UpValuesCount];
                for (int i = 0; i < UpValuesCount; i++)
                {
                    UpvalueNames[i] = new UpvalueName(br, LuaVersion.Lua53Smash);
                }
            }

            public Function(LuaFile file, LuaVersion version, BinaryReaderEx br)
            {
                FunctionID = file.GetNextFunctionId();
                if (version == LuaVersion.Lua53Smash)
                {
                    ReadLua53Smash(file, br);
                }
                else if (version == LuaVersion.Lua50)
                {
                    Name = LuaFile.ReadLuaString(br, version);
                    LineDefined = br.ReadInt32();
                    Nups = br.ReadByte();
                    NumParams = br.ReadByte();
                    IsVarArg = br.ReadByte();
                    MaxStackSize = br.ReadByte();
                    SizeLineInfo = br.ReadInt32();
                    // Eat line numbers
                    br.ReadInt32s(SizeLineInfo);
                    LocalVarsCount = br.ReadInt32();
                    Locals = new Local[LocalVarsCount];
                    LocalMap = new Dictionary<int, List<Local>>();
                    for (int i = 0; i < LocalVarsCount; i++)
                    {
                        Locals[i] = new Local(br, version);
                        if (!Locals[i].Name.StartsWith("("))
                        {
                            if (!LocalMap.ContainsKey(Locals[i].Start))
                            {
                                LocalMap[Locals[i].Start] = new List<Local>();
                            }
                            LocalMap[Locals[i].Start].Add(Locals[i]);
                        }
                    }
                    UpValuesCount = br.ReadInt32();
                    UpvalueNames = new UpvalueName[UpValuesCount];
                    for (int i = 0; i < UpValuesCount; i++)
                    {
                        UpvalueNames[i] = new UpvalueName(br, version);
                    }
                    int constantsCount = br.ReadInt32();
                    Constants = new Constant[constantsCount];
                    for (int i = 0; i < constantsCount; i++)
                    {
                        Constants[i] = new Constant(file, br, version);
                    }
                    int funcCount = br.ReadInt32();
                    ChildFunctions = new Function[funcCount];
                    for (int i = 0; i < funcCount; i++)
                    {
                        ChildFunctions[i] = new Function(file, version, br);
                    }
                    int bytecodeCount = br.ReadInt32();
                    Bytecode = br.ReadBytes(bytecodeCount * 4);
                }
                else if (version == LuaVersion.Lua51HKS)
                {
                    // Thanks @horkrux for reverse engineering this
                    br.ReadInt32();
                    NumParams = br.ReadUInt32();
                    br.ReadByte(); // Unk
                    NumSlots = br.ReadUInt32();
                    br.ReadUInt32(); // unk
                    int bytecodeCount = br.ReadInt32();
                    br.Pad(4);
                    Bytecode = br.ReadBytes(bytecodeCount * 4);
                    int constantsCount = br.ReadInt32();
                    ConstantsHKS = new ConstantHKS[constantsCount];
                    for (int i = 0; i < constantsCount; i++)
                    {
                        ConstantsHKS[i] = new ConstantHKS(file, br);
                    }
                    br.ReadInt32(); // unk
                    br.ReadInt32(); // unk
                    LocalVarsCount = br.ReadInt32();
                    UpValuesCount = br.ReadInt32();
                    br.ReadInt32(); // line begin
                    br.ReadInt32(); // line end
                    Path = ReadLuaString(br, version);
                    Name = ReadLuaString(br, version);
                    // Eat line numbers
                    br.ReadInt32s(bytecodeCount);
                    Locals = new Local[LocalVarsCount];
                    LocalMap = new Dictionary<int, List<Local>>();
                    for (int i = 0; i < LocalVarsCount; i++)
                    {
                        Locals[i] = new Local(br, version);
                        if (!Locals[i].Name.StartsWith("("))
                        {
                            if (!LocalMap.ContainsKey(Locals[i].Start))
                            {
                                LocalMap[Locals[i].Start] = new List<Local>();
                            }
                            LocalMap[Locals[i].Start].Add(Locals[i]);
                        }
                    }
                    UpvalueNames = new UpvalueName[UpValuesCount];
                    for (int i = 0; i < UpValuesCount; i++)
                    {
                        UpvalueNames[i] = new UpvalueName(br, version);
                    }
                    int funcCount = br.ReadInt32();
                    ChildFunctions = new Function[funcCount];
                    for (int i = 0; i < funcCount; i++)
                    {
                        ChildFunctions[i] = new Function(file, version, br);
                    }
                }
            }

            public List<Local> LocalsAt(int i)
            {
                if (LocalMap.ContainsKey(i))
                {
                    return LocalMap[i];
                }
                return null;
            }

            public override string ToString()
            {
                if (Name != null && Name != "")
                {
                    return Name;
                }
                return base.ToString();
            }
        }

        public Header LuaHeader;
        public Function MainFunction;

        public LuaFile(BinaryReaderEx br)
        {
            LuaHeader = new Header(br);
            Version = LuaHeader.LuaVersion;
            if (Version == LuaVersion.Lua51HKS)
            {
                br.BigEndian = true;
                br.Position = 0xee; // lel
            }
            else if (Version == LuaVersion.Lua53Smash)
            {
                // read "upval size"
                br.ReadByte();
            }
            MainFunction = new Function(this, Version, br);
        }
    }
}
