using System.Text;
using LuaCompiler.Compilers;
using LuaDecompilerCore.LanguageDecompilers;
using LuaDecompilerTestFramework;
using Microsoft.VisualStudio.TestPlatform.Utilities;

namespace LuaIntegrationTests;

public class Lua50TheoryData : TheoryData<ITestCase>
{
    public Lua50TheoryData()
    {
        var entries = Directory.GetFiles("lua\\50", "*.lua", SearchOption.AllDirectories);
        var entriesShared = Directory.GetFiles("lua\\shared", "*.lua", SearchOption.AllDirectories);
        foreach (var entry in entries)
        {
            Add(new SourceFileTestCase(entry));
        }
        foreach (var entry in entriesShared)
        {
            Add(new SourceFileTestCase(entry));
        }
        
        if (Directory.Exists("output/lua50"))
            Directory.Delete("output/lua50", true);
        Directory.CreateDirectory("output/lua50");
    }
}

public class LuaHksTheoryData : TheoryData<ITestCase>
{
    public LuaHksTheoryData()
    {
        var entries = Directory.GetFiles("lua\\hks", "*.lua", SearchOption.AllDirectories);
        var entriesShared = Directory.GetFiles("lua\\shared", "*.lua", SearchOption.AllDirectories);

        foreach (var entry in entries)
        {
            Add(new SourceFileTestCase(entry));
        }
        foreach (var entry in entriesShared)
        {
            Add(new SourceFileTestCase(entry));
        }
        
        if (Directory.Exists("output/hks"))
            Directory.Delete("output/hks", true);
        Directory.CreateDirectory("output/hks");
    }
}

public class LuaIntegrationTests
{
    public static Lua50TheoryData Lua50TestData = new();
    public static LuaHksTheoryData LuaHksTestData = new();
    
    [Theory]
    [MemberData(nameof(Lua50TestData))]
    public void Lua50Tests(ITestCase test)
    {
        var tester = new DecompilationTester(
            new Lua50Decompiler(),
            new Lua50Compiler(),
            Encoding.UTF8,
            new DecompilationTesterOptions
            {
                DumpPassIr = true,
                DumpCfg = true,
                MultiThreaded = false,
                HandleDecompilationExceptions = false,
                IgnoreDebugInfo = true
            });
        tester.AddTestCase(test);
        var result = tester.Execute();
        TestUtilities.WriteTestResultArtifactsToDirectory(
            result, "output/lua50", Encoding.UTF8, true, false);
        Assert.Equal(TestCaseError.Success, result[0].Error);
    }
    
    [Theory]
    [MemberData(nameof(LuaHksTestData))]
    public void LuaHksTests(ITestCase test)
    {
        var tester = new DecompilationTester(
            new HksDecompiler(),
            new LuaHavokScriptCompiler(),
            Encoding.UTF8,
            new DecompilationTesterOptions
            {
                DumpPassIr = true,
                DumpCfg = true,
                MultiThreaded = false,
                HandleDecompilationExceptions = true,
                IgnoreDebugInfo = true
            });
        tester.AddTestCase(test);
        var result = tester.Execute();
        TestUtilities.WriteTestResultArtifactsToDirectory(
            result, "output/hks", Encoding.UTF8, true, false);
        Assert.Equal(TestCaseError.Success, result[0].Error);
    }
}