using System.Runtime.InteropServices;
using System.Text;
using static LuaCompiler.Bindings.Lua50;

namespace LuaCompiler.Compilers;

public class Lua50Compiler : ICompiler
{
    [UnmanagedCallersOnly]
    private static unsafe int Writer(LuaState* state, void *data, ulong size, nint userData)
    {
        if (GCHandle.FromIntPtr(userData).Target is not MemoryStream stream)
            return 0;
        stream.Write(new ReadOnlySpan<byte>(data, (int)size));
        return 1;
    }
    
    public unsafe byte[] CompileSource(string source, Encoding encoding)
    {
        var state = LuaOpen();
        try
        {
            if (LuaLLoadString(state, source, null, encoding) != 0)
            {
                var pError = LuaToString(state, -1);
                if (pError == null)
                    throw new CompileException("No error message");
                
                // Lua 5.0 returns ascii strings *sigh*
                var count = 0;
                while (pError[count] != 0 && count < 1000)
                    count++;
                throw new CompileException(encoding.GetString(pError, count));
            }
            
            // Get the main function prototype
            var proto = ((LClosure*)LuaToPointer(state, -1))->P;
            
            // Create a memory stream to write the output to and pin it so it can be passed
            using var stream = new MemoryStream();
            var streamHandle = GCHandle.Alloc(stream);

            LuaUDump(state, proto, &Writer, GCHandle.ToIntPtr(streamHandle));
            return stream.ToArray();
        }
        finally
        {
            LuaClose(state);
        }
    }
}