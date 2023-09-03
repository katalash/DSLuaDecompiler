using System.Text;
using LuaCompiler;
using LuaDecompilerCore;
using LuaDecompilerCore.IR;
using LuaDecompilerCore.Utilities;

namespace LuaDecompilerTestFramework;

/// <summary>
/// Options for the decompiler tester
/// </summary>
public sealed class DecompilationTesterOptions
{
    /// <summary>
    /// All decompilations will also dump the Ir for each pass
    /// </summary>
    public bool DumpPassIr { get; init; }
    
    /// <summary>
    /// The control flow graphs will be dumped for all passes that mutate the Cfg
    /// </summary>
    public bool DumpCfg { get; init; }

    /// <summary>
    /// Decompilation exceptions will be handled and reported back as errors instead of
    /// being fatal. Disabling this might be useful for when actively debugging.
    /// </summary>
    public bool HandleDecompilationExceptions { get; init; } = true;

    /// <summary>
    /// Test cases will be processed in parallel.
    /// </summary>
    public bool MultiThreaded { get; init; } = true;

    /// <summary>
    /// Ignore debug info in the compiled Lua file if it exists.
    /// </summary>
    public bool IgnoreDebugInfo { get; init; } = false;
}

/// <summary>
/// A test engine for the Lua decompiler. It is designed to take a set of input test cases, which
/// are either source or compiled lua files, and test correctness of the decompiler and give further
/// information to pinpoint where bad decompilation is happening. It performs the following steps:
/// 1. If the test lua is provided in source form, then the Lua is compiled.
/// 2. The compiled lua is then decompiled.
/// 3. The decompiled lua is then compiled again.
/// 4. The recompiled lua's bytecode is then compared function by function with the original bytecode.
/// 5. If there are mismatches then the mismatching function Ids are reported.
/// 6. In the future CFG comparison and bytecode dumping can be done to further try and pinpoint errors.
/// </summary>
public sealed class DecompilationTester
{
    private readonly ICompiler _compiler;
    private readonly ILanguageDecompiler _languageDecompiler;
    private readonly LuaDecompiler _decompiler;
    private readonly Encoding _encoding;
    private readonly List<ITestCase> _testCases = new();
    private readonly bool _multiThread;

    public DecompilationTester(ILanguageDecompiler decompiler,
        ICompiler compiler,
        Encoding encoding,
        DecompilationTesterOptions options)
    {
        _compiler = compiler;
        _languageDecompiler = decompiler;
        var decompilationOptions = new DecompilationOptions
        {
            OutputDebugComments = true,
            ExtraValidation = true,
            CatchPassExceptions = options.HandleDecompilationExceptions || options.MultiThreaded,
            DumpIrPasses = options.DumpPassIr ? new HashSet<string> { "all" } : new HashSet<string>(),
            DumpDotGraphPasses = options.DumpCfg ? new HashSet<string> { "cfg-mutated" } : new HashSet<string>(),
            IgnoreDebugInfo = options.IgnoreDebugInfo
        };
        _decompiler = new LuaDecompiler(decompilationOptions);
        _encoding = encoding;
        _multiThread = options.MultiThreaded;
    }

    /// <summary>
    /// Adds a test case that will be tested
    /// </summary>
    /// <param name="testCase">The test case to add</param>
    public void AddTestCase(ITestCase testCase)
    {
        _testCases.Add(testCase);
    }

    /// <summary>
    /// Executes all the test cases added and returns an array of the results in the order that the
    /// test cases were added.
    /// </summary>
    /// <returns></returns>
    public TestCaseResult[] Execute()
    {
        var results = new TestCaseResult[_testCases.Count];

        if (_multiThread)
        {
            Parallel.For(0, _testCases.Count, (i, _) =>
            {
                results[i] = ExecuteTestCase(_testCases[i]);
            });
        }
        else
        {
            for (var i = 0; i < _testCases.Count; i++)
            {
                results[i] = ExecuteTestCase(_testCases[i]);
            }
        }

        return results;
    }
    
    private static bool CompareLuaBytecode(
        byte[] a,
        byte[] b,
        LuaDecompiler decompiler,
        ILanguageDecompiler languageDecompiler,
        out int total, 
        out List<int> mismatchedFunctionIds,
        out List<string?> mismatchedCompiledDisassembledFunctions,
        out List<string?> mismatchedRecompiledDisassembledFunctions)
    {
        var br1 = new BinaryReaderEx(false, a);
        var br2 = new BinaryReaderEx(false, b);
        var luaFile1 = new LuaFile(br1);
        var luaFile2 = new LuaFile(br2);
        var totalCounter = 0;
        var mismatchedIds = new List<int>();
        var mismatchedCompiledDisassembled = new List<string?>();
        var mismatchedRecompiledDisassembled = new List<string?>();
        
        bool CompareFunction(LuaFile.Function f1, LuaFile.Function f2)
        {
            var functionMatched = true;
            if (f1.ChildFunctions.Length != f2.ChildFunctions.Length || 
                !f1.Bytecode.SequenceEqual(f2.Bytecode))
            {
                mismatchedIds.Add(f2.FunctionId);
                mismatchedRecompiledDisassembled.Add(decompiler.DisassembleLuaFunction(languageDecompiler, f1));
                mismatchedCompiledDisassembled.Add(decompiler.DisassembleLuaFunction(languageDecompiler, f2));
                functionMatched = false;
            }

            for (var index = 0; index < f1.ChildFunctions.Length; ++index)
            {
                if (index >= f2.ChildFunctions.Length ||
                    !CompareFunction(f1.ChildFunctions[index], f2.ChildFunctions[index]))
                    functionMatched = false;
            }

            totalCounter++;
            return functionMatched;
        }
        
        var matched = CompareFunction(luaFile1.MainFunction, luaFile2.MainFunction);
        mismatchedFunctionIds = mismatchedIds;
        mismatchedCompiledDisassembledFunctions = mismatchedCompiledDisassembled;
        mismatchedRecompiledDisassembledFunctions = mismatchedRecompiledDisassembled;
        total = totalCounter;
        return matched;
    }
    
    private TestCaseResult ExecuteTestCase(ITestCase testCase)
    {
        if (testCase is not SourceFileTestCase && 
            testCase is not SourceStringTestCase && 
            testCase is not CompiledFileTestCase && 
            testCase is not CompiledBytesTestCase)
        {
            throw new Exception("Invalid test case");
        }
        
        TestCaseResult result = new TestCaseResult();
        byte[] compiledBytes;
        
        // If provided in source form we need to compile it first
        if (testCase is SourceFileTestCase or SourceStringTestCase)
        {
            string source;
            if (testCase is SourceFileTestCase fileTestCase)
            {
                result.Name = Path.GetFileName(fileTestCase.Path);
                source = File.ReadAllText(fileTestCase.Path, _encoding);
            }
            else if (testCase is SourceStringTestCase stringTestCase)
            {
                result.Name = stringTestCase.Name;
                source = stringTestCase.SourceString;
            }
            else
            {
                throw new Exception("Unreachable");
            }

            result.Source = source;
            
            // Compile the lua
            try
            {
                compiledBytes = _compiler.CompileSource(source, _encoding);
            }
            catch (CompileException e)
            {
                result.Error = TestCaseError.ErrorCompilationFailed;
                result.ErrorMessage = e.Message;
                return result;
            }
        }
        else
        {
            if (testCase is CompiledFileTestCase fileTestCase)
            {
                result.Name = Path.GetFileName(fileTestCase.Path);
                compiledBytes = File.ReadAllBytes(fileTestCase.Path);
            }
            else if (testCase is CompiledBytesTestCase bytesTestCase)
            {
                result.Name = bytesTestCase.Name;
                compiledBytes = bytesTestCase.CompiledBytes;
            }
            else
            {
                throw new Exception("Unreachable");
            }
        }

        // We have the compiled bytes. Now we need to decompile
        result.CompiledBytes = compiledBytes;
        var luaFile = new LuaFile(new BinaryReaderEx(false, compiledBytes));
        var irFunction = new Function(luaFile.MainFunction.FunctionId);
        var decompilationResult =
            _decompiler.DecompileLuaFunction(_languageDecompiler, irFunction, luaFile.MainFunction);
        result.DecompilationResult = decompilationResult;
        result.DecompiledSource = decompilationResult.DecompiledSource;
        if (decompilationResult.DecompiledSource == null)
        {
            // Disassemble everything
            var disassembledFunctions = new List<string?>();
            var disassembledFunctionIds = new List<int>();

            void Visit(LuaFile.Function function)
            {
                disassembledFunctions.Add(_decompiler.DisassembleLuaFunction(_languageDecompiler, function));
                disassembledFunctionIds.Add(function.FunctionId);
                foreach (var child in function.ChildFunctions)
                {
                    Visit(child);
                }
            }
            Visit(luaFile.MainFunction);
            
            result.Error = TestCaseError.ErrorDecompilationFailed;
            result.ErrorMessage = decompilationResult.ErrorMessage;
            result.MismatchedFunctionIds = disassembledFunctionIds.ToArray();
            result.MismatchedCompiledDisassembledFunctions = disassembledFunctions.ToArray();
            return result;
        }
        
        // Attempt to recompile the decompiled source
        byte[] recompiledBytes;
        try
        {
            recompiledBytes = _compiler.CompileSource(decompilationResult.DecompiledSource, _encoding);
        }
        catch (CompileException e)
        {
            // Disassemble everything (yes copy paste bad)
            var disassembledFunctions = new List<string?>();
            var disassembledFunctionIds = new List<int>();

            void Visit(LuaFile.Function function)
            {
                disassembledFunctions.Add(_decompiler.DisassembleLuaFunction(_languageDecompiler, function));
                disassembledFunctionIds.Add(function.FunctionId);
                foreach (var child in function.ChildFunctions)
                {
                    Visit(child);
                }
            }
            Visit(luaFile.MainFunction);
            
            result.Error = TestCaseError.ErrorRecompilationFailed;
            result.ErrorMessage = e.Message;
            result.MismatchedFunctionIds = disassembledFunctionIds.ToArray();
            result.MismatchedCompiledDisassembledFunctions = disassembledFunctions.ToArray();
            return result;
        }

        result.RecompiledBytes = recompiledBytes;
        
        // Now compare the bytecode of the original with the recompiled
        if (!CompareLuaBytecode(recompiledBytes,
                compiledBytes,
                _decompiler,
                _languageDecompiler,
                out var total,
                out var mismatchedFunctionIds,
                out var mismatchedCompiledDisassembledFunctions,
                out var mismatchedRecompiledDisassembledFunctions))
        {
            result.Error = TestCaseError.ErrorMismatch;
            result.MismatchedFunctionIds = mismatchedFunctionIds.ToArray();
            result.MismatchedCompiledDisassembledFunctions = mismatchedCompiledDisassembledFunctions.ToArray();
            result.MismatchedRecompiledDisassembledFunctions = mismatchedRecompiledDisassembledFunctions.ToArray();
            result.TotalFunctionIds = total;
            return result;
        }

        result.Error = TestCaseError.Success;
        return result;
    }
}