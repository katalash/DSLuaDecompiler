using System.Runtime.InteropServices;
using System.Text;
using static LuaCompiler.Utilities;

namespace LuaCompiler.Bindings;

/// <summary>
/// Partial bindings for Lua 5.0 dll to be able to compile lua functions
/// </summary>
internal static unsafe partial class Lua50
{
    public struct TObject
    {
        public int Tt;
        public ulong Value;
    }
    
    public struct Proto { }

    public struct CClosure
    {
        public void* Next;
        public byte Tt;
        public byte Marked;
        public byte IsC;
        public byte NUpValues;
        public void* GcList;
        public void* F;
        public TObject UpValue;
    }

    public struct LClosure
    {
        public void* Next;
        public byte Tt;
        public byte Marked;
        public byte IsC;
        public byte NUpValues;
        public void* GcList;
        public Proto* P;
        public TObject G;
        public void* UpValues;
    }

    public struct LuaState { }
    
    [LibraryImport(
        "Lib/lua502.dll",
        EntryPoint = "lua_open",
        StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial LuaState* LuaOpen();
    
    [LibraryImport(
        "Lib/lua502.dll",
        EntryPoint = "lua_close",
        StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial void LuaClose(LuaState *state);
    
    [LibraryImport(
        "Lib/lua502.dll",
        EntryPoint = "lua_topointer",
        StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial void* LuaToPointer(LuaState *state, int index);
    
    [LibraryImport(
        "Lib/lua502.dll",
        EntryPoint = "lua_tostring",
        StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial byte* LuaToString(LuaState *state, int idx);
    
    [LibraryImport(
        "Lib/lua502.dll",
        EntryPoint = "luaL_loadbuffer",
        StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial int LuaLLoadBuffer(LuaState *state, byte *buffer, ulong size, byte* name);

    public static int LuaLLoadString(LuaState *state, string s, string? name, Encoding encoding)
    {
        var stringBytes = encoding.GetBytes(s);
        var nameBytes = name != null ? StringToNullTerminatedCString(name, encoding) : null;
        fixed (byte* pStringBytes = stringBytes, pNameBytes = nameBytes)
        {
            return LuaLLoadBuffer(
                state, 
                pStringBytes, 
                (ulong)stringBytes.Length, 
                nameBytes != null ? pNameBytes : pStringBytes);
        }
    }
    
    [LibraryImport(
        "Lib/lua502.dll",
        EntryPoint = "luaU_dump",
        StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial void LuaUDump(
        LuaState *state,
        Proto *main, 
        delegate* unmanaged<LuaState*, void*, ulong, nint, int> chunkWriter, 
        nint data);
}