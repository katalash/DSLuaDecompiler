using System.Text;

namespace LuaCompiler;

public static class Utilities
{
    public static byte[] StringToNullTerminatedCString(string? s, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(s);
        
        var byteCount = encoding.GetByteCount(s) + 1;
        var bytes = new byte[byteCount];
        encoding.GetBytes(s, 0, s.Length, bytes, 0);
        bytes[^1] = 0;
        return bytes;
    }
}