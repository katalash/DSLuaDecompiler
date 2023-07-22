using LuaCompiler;

namespace LuaDecompilerTestFramework;

/// <summary>
/// Base interface for a test case that can be processed by the tester
/// </summary>
public interface ITestCase { }

/// <summary>
/// A test case that tests a source lua file on disk
/// </summary>
/// <param name="Path">Path to the source lua file</param>
public record SourceFileTestCase(string Path) : ITestCase;

/// <summary>
/// A test case that tests lua source from a string
/// </summary>
/// <param name="SourceString"></param>
public record SourceStringTestCase(string Name, string SourceString) : ITestCase;

/// <summary>
/// A test case that tests a compiled lua file on disk
/// </summary>
/// <param name="Path">Path to the compiled lua file</param>
public record CompiledFileTestCase(string Path) : ITestCase;

/// <summary>
/// A test case that tests compiled lua passed in with a byte array
/// </summary>
/// <param name="CompiledBytes"></param>
public record CompiledBytesTestCase(string Name, byte[] CompiledBytes) : ITestCase;