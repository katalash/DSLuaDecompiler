using System.Text;
using LuaCompiler.Compilers;
using LuaDecompilerCore.LanguageDecompilers;
using LuaDecompilerTestFramework;

namespace LuaIntegrationTests;

public class Lua50TheoryData : TheoryData<ITestCase>
{
    public Lua50TheoryData()
    {
        var entries = Directory.GetFiles("lua\\50", "*.lua", SearchOption.AllDirectories);
        foreach (var entry in entries)
        {
            Add(new SourceFileTestCase(entry));
        }
    }
}

public class LuaIntegrationTests
{
    public static Lua50TheoryData Tests = new Lua50TheoryData();
    
    [Theory]
    [MemberData(nameof(Tests))]
    public void Lua50Tests(ITestCase test)
    {
        var tester = new DecompilationTester(
            new Lua50Decompiler(),
            new Lua50Compiler(),
            Encoding.UTF8,
            new DecompilationTesterOptions
            {
                DumpPassIr = false,
                DumpCfg = false,
                MultiThreaded = false,
                HandleDecompilationExceptions = false
            });
        tester.AddTestCase(test);
        var result = tester.Execute();
        Assert.Equal(TestCaseError.Success, result[0].Error);
    }
}