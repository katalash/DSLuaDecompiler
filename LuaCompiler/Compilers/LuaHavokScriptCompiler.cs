using System.Runtime.InteropServices;
using System.Text;
using static LuaCompiler.Bindings.LuaHavokScript;

namespace LuaCompiler.Compilers;

public class LuaHavokScriptCompiler : ICompiler
{
    [UnmanagedCallersOnly]
    private static unsafe int Writer(LuaState* state, void *data, ulong size, nint userData)
    {
        if (GCHandle.FromIntPtr(userData).Target is not MemoryStream stream)
            return 1;
        stream.Write(new ReadOnlySpan<byte>(data, (int)size));
        return 0;
    }

    private class CompilerState
    {
        private readonly string _source;
        private readonly Encoding _encoding;
        public byte[]? Result;

        public CompilerState(string source, Encoding encoding)
        {
            _source = source;
            _encoding = encoding;
        }
        
        public unsafe int LuaMain(LuaState* state)
        {
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
            Result = stream.ToArray();
            return 0;
        }
    }
    
    [UnmanagedCallersOnly]
    private static unsafe int LuaMainUnmanaged(LuaState* state)
    {
        // Unpack ourself from the user data and call the managed version
        if (GCHandle.FromIntPtr((IntPtr)LuaToUserData(state, 1)).Target is not CompilerState compiler)
            throw new Exception("Unpacking object failed");
        return compiler.LuaMain(state);
    }
    
    public unsafe byte[] CompileSource(string source, Encoding encoding)
    {
        var state = LuaLNewState();
        try
        {
            // Save data for callback
            var compilerState = new CompilerState(source, encoding);
            
            // We have to cpcall into a C Lua function we define to be able to properly dump the module. Since
            // unmanaged function pointer targets have to be static, we must pin ourself and pass ourself as user
            // data.
            var handle = GCHandle.Alloc(compilerState);
            if (LuaCpCall(state, &LuaMainUnmanaged, GCHandle.ToIntPtr(handle)) != 0)
            {
                var pError = LuaToLString(state, -1, out var length);
                if (pError == null)
                    throw new CompileException("No error message");
                throw new CompileException(encoding.GetString(pError, (int)length));
            }

            if (compilerState.Result == null)
                throw new CompileException("Result is null but no error");
            
            return compilerState.Result;
        }
        finally
        {
            LuaClose(state);
        }
    }
}