namespace LuaDecompilerCore.IR;

/// <summary>
/// Base interface for all IR classes to support things like visitors
/// </summary>
public interface IIrNode
{
    public void Accept(IIrVisitor visitor);
}