using System.Runtime.InteropServices;
using System.Text;
using LuaCompiler;

namespace LuaCompiler.Bindings;

/// <summary>
/// Partial bindings for HavokScript Dll (specifically the one that is shipped with Civilization VI)
/// </summary>
internal unsafe partial class LuaHavokScript
{
    public struct TValue
    {
        public ulong Value;
        public int TT;
    }
    
    /// <summary>
    /// Not a complete definition but enough to extract what we need
    /// </summary>
    public struct LuaState
    {
        public void* Next;  // 0x00
        public byte Tt;     // 0x08
        public byte Marked; // 0x09
        public byte Status; // 0x0A
        public TValue* Top; // 0x10
        // ...
        public fixed byte Unk[0x48]; // 0x18
        public ulong UnkFunctionPointer;
    }

    public struct HksCompilerSettings
    {
        public ulong EmitStruct;
        public void* Strip;
        public int GlobalMemoization;
        public int Unk14;
        public int BytecodeSharingFormat;
        public int IntLiteralOptions;
        public ulong UnkFunctionPointer;
    }
    
    [LibraryImport(
        "LuaNative/HavokScript_FinalRelease.dll",
        EntryPoint = "?luaL_newstate@@YAPEAUlua_State@@XZ",
        StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial LuaState* LuaLNewState();
    
    [LibraryImport(
        "LuaNative/HavokScript_FinalRelease.dll",
        EntryPoint = "?lua_close@@YAXPEAUlua_State@@@Z",
        StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial void LuaClose(LuaState *state);
    
    [LibraryImport(
        "LuaNative/HavokScript_FinalRelease.dll",
        EntryPoint = "?lua_cpcall@@YAHPEAUlua_State@@P6AH0@ZPEAX@Z",
        StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial int LuaCpCall(
        LuaState *state,
        delegate* unmanaged<LuaState*, int> function,
        nint userData);
    
    [LibraryImport(
        "LuaNative/HavokScript_FinalRelease.dll",
        EntryPoint = "?lua_tolstring@@YAPEBDPEAUlua_State@@HPEA_K@Z",
        StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial byte* LuaToLString(LuaState *state, int idx, out ulong length);
    
    [LibraryImport(
        "LuaNative/HavokScript_FinalRelease.dll",
        EntryPoint = "?lua_touserdata@@YAPEAXPEAUlua_State@@H@Z",
        StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial void* LuaToUserData(LuaState *state, int idx);
    
    [LibraryImport(
        "LuaNative/HavokScript_FinalRelease.dll",
        EntryPoint = "?hksL_loadbuffer@@YAHPEAUlua_State@@AEBUHksCompilerSettings@@PEBD_K2@Z",
        StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial int HksLLoadBuffer(
        LuaState *state, 
        HksCompilerSettings *settings, 
        byte *buffer, 
        ulong length, 
        byte *name);
    
    public static int HksLLoadString(LuaState *state,
        HksCompilerSettings *settings,
        string s,
        string? name,
        Encoding encoding)
    {
        var stringBytes = encoding.GetBytes(s);
        var nameBytes = name != null ? Utilities.StringToNullTerminatedCString(name, encoding) : null;
        fixed (byte* pStringBytes = stringBytes, pNameBytes = nameBytes)
        {
            return HksLLoadBuffer(
                state,
                settings,
                pStringBytes, 
                (ulong)stringBytes.Length, 
                nameBytes != null ? pNameBytes : pStringBytes);
        }
    }
    
    [LibraryImport(
        "LuaNative/HavokScript_FinalRelease.dll",
        EntryPoint = "?hks_dump@@YAHPEAUlua_State@@P6AH0PEBX_KPEAX@Z3W4HksBytecodeStrippingLevel@@@Z",
        StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial int HksDump(
        LuaState *state, 
        delegate* unmanaged<LuaState*, void*, ulong, nint, int> writer, 
        nint data, 
        int strippingLevel);
}