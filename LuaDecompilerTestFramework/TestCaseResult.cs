using LuaDecompilerCore;

namespace LuaDecompilerTestFramework;

/// <summary>
/// Error classification for a test case
/// </summary>
public enum TestCaseError
{
    /// <summary>
    /// Test case executed successfully
    /// </summary>
    Success,
    
    /// <summary>
    /// The source lua for the test case failed to compile
    /// </summary>
    ErrorCompilationFailed,
    
    /// <summary>
    /// The compiled lua failed to decompile
    /// </summary>
    ErrorDecompilationFailed,
    
    /// <summary>
    /// The decompiled lua source fails to recompile
    /// </summary>
    ErrorRecompilationFailed,
    
    /// <summary>
    /// The recompiled lua source has a mismatch with the original lua
    /// </summary>
    ErrorMismatch,
}

/// <summary>
/// The result payload of a test case
/// </summary>
public sealed class TestCaseResult
{
    /// <summary>
    /// The name of the test case.
    /// </summary>
    public string Name { get; internal set; } = "";

    /// <summary>
    /// The error category of the test case.
    /// </summary>
    public TestCaseError Error { get; internal set; } = TestCaseError.Success;
    
    /// <summary>
    /// The error message for a test case if there was a decompilation error
    /// </summary>
    public string? ErrorMessage { get; internal set; }
    
    /// <summary>
    /// The original source if available
    /// </summary>
    public string? Source { get; internal set; }
    
    /// <summary>
    /// The bytes for the compiled lua bytes
    /// </summary>
    public byte[]? CompiledBytes { get; internal set; }
    
    /// <summary>
    /// The source for the decompiled lua source
    /// </summary>
    public string? DecompiledSource { get; internal set; }
    
    /// <summary>
    /// The decompilation result returned from the decompiler
    /// </summary>
    public DecompilationResult? DecompilationResult { get; internal set; }
    
    /// <summary>
    /// The bytes for the recompiled lua bytes
    /// </summary>
    public byte[]? RecompiledBytes { get; internal set; }
    
    /// <summary>
    /// Function Ids where the bytecode between the original compiled bytecode and the recompiled
    /// bytecode mismatch
    /// </summary>
    public int[]? MismatchedFunctionIds { get; internal set; }
    
    /// <summary>
    /// Total number of function Ids
    /// </summary>
    public int? TotalFunctionIds { get; internal set; }
}