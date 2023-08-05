using System.Runtime.InteropServices;
using System.Text;
using LuaCompiler.Bindings;
using static LuaCompiler.Bindings.LuaHavokScript;

namespace LuaCompiler.Compilers;

public class LuaHavokScriptCompiler : ICompiler
{
    private string? _source = null;
    private Encoding? _encoding = null;
    private byte[]? _result = null;

    [UnmanagedCallersOnly]
    private static unsafe int Writer(LuaState* state, void *data, ulong size, nint userData)
    {
        if (GCHandle.FromIntPtr(userData).Target is not MemoryStream stream)
            return 1;
        stream.Write(new ReadOnlySpan<byte>(data, (int)size));
        return 0;
    }
    
    private unsafe int LuaMain(LuaState* state)
    {
        if (_source == null || _encoding == null)
            throw new Exception("Inputs not set");
        
        var settings = new HksCompilerSettings
        {
            EmitStruct = 1,
            Strip = null,
            BytecodeSharingFormat = 0,
            IntLiteralOptions = 0,
            UnkFunctionPointer = state->UnkFunctionPointer,
            GlobalMemoization = 1,
            Unk14 = 0,
        };
        if (HksLLoadString(state, &settings, _source, null, _encoding) != 0)
        {
            var pError = LuaToLString(state, -1, out var length);
            if (pError == null)
                throw new CompileException("No error message");
            throw new CompileException(_encoding.GetString(pError, (int)length));
        }
        
        using var stream = new MemoryStream();
        var streamHandle = GCHandle.Alloc(stream);
        HksDump(state, &Writer, GCHandle.ToIntPtr(streamHandle), 0);
        _result = stream.ToArray();
        return 0;
    }
    
    [UnmanagedCallersOnly]
    private static unsafe int LuaMainUnmanaged(LuaState* state)
    {
        // Unpack ourself from the user data and call the managed version
        var compiler = GCHandle.FromIntPtr((IntPtr)LuaToUserData(state, 1)).Target as LuaHavokScriptCompiler;
        if (compiler == null)
            throw new Exception("Unpacking object failed");
        return compiler.LuaMain(state);
    }
    
    public unsafe byte[] CompileSource(string source, Encoding encoding)
    {
        var state = LuaLNewState();
        try
        {
            // Save data for callback
            _source = source;
            _encoding = encoding;
            
            // We have to cpcall into a C Lua function we define to be able to properly dump the module. Since
            // unmanaged function pointer targets have to be static, we must pin ourself and pass ourself as user
            // data.
            var handle = GCHandle.Alloc(this);
            if (LuaCpCall(state, &LuaMainUnmanaged, GCHandle.ToIntPtr(handle)) != 0)
            {
                var pError = LuaToLString(state, -1, out var length);
                if (pError == null)
                    throw new CompileException("No error message");
                throw new CompileException(encoding.GetString(pError, (int)length));
            }

            if (_result == null)
                throw new CompileException("Result is null but no error");
            
            return _result;
        }
        finally
        {
            LuaClose(state);
        }
    }
}