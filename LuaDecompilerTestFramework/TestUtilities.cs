using System.Text;

namespace LuaDecompilerTestFramework;

/// <summary>
/// Set of utility functions to help with testing
/// </summary>
public static class TestUtilities
{
    /// <summary>
    /// Adds all the lua source files in a directory to the tester
    /// </summary>
    /// <param name="path">Path to directory</param>
    /// <param name="extension">The extension of the lua files</param>
    /// <param name="tester">The tester to add to</param>
    public static void AddLuaSourceDirectoryToTester(string path, string extension, DecompilationTester tester)
    {
        var files = Directory.GetFileSystemEntries(path, $"*.{extension}").ToList();
        foreach (var file in files)
        {
            tester.AddTestCase(new SourceFileTestCase(file));
        }
    }
    
    /// <summary>
    /// Adds all the compiled lua files in a directory to the tester
    /// </summary>
    /// <param name="path">Path to directory</param>
    /// <param name="extension">The extension of the lua files</param>
    /// <param name="tester">The tester to add to</param>
    public static void AddCompiledLuaDirectoryToTester(string path, string extension, DecompilationTester tester)
    {
        var files = Directory.GetFileSystemEntries(path, $"*.{extension}").ToList();
        foreach (var file in files)
        {
            tester.AddTestCase(new CompiledFileTestCase(file));
        }
    }
    
    /// <summary>
    /// Adds all the compiled luabnd archive lua files in a directory to the tester
    /// </summary>
    /// <param name="path">Path to directory</param>
    /// <param name="tester">The tester to add to</param>
    public static void AddCompiledLuaBndDirectoryToTester(string path, DecompilationTester tester)
    {
        var archives = Directory.GetFileSystemEntries(path, "*.luabnd.dcx").ToList();
        foreach (var archive in archives)
        {
            foreach (var file in SoulsFormats.BND4.Read(archive).Files)
            {
                if (file.Name.EndsWith(".lua"))
                {
                    tester.AddTestCase(new CompiledBytesTestCase(
                            $"{Path.GetFileNameWithoutExtension(archive)}/{Path.GetFileName(file.Name)}", file.Bytes));
                }
            }
        }
    }

    /// <summary>
    /// Writes out relevant artifacts (such as the compiled lua, decompiled lua, CFG dumps, IR, etc) for all the
    /// test cases.
    /// </summary>
    /// <param name="results">The test case results</param>
    /// <param name="directory">The directory to write to</param>
    /// <param name="encoding">The encoding to use when writing</param>
    /// <param name="writeEverything">Writes everything instead of the most relevant files for the failure</param>
    /// <param name="deleteDirectory"></param>
    public static void WriteTestResultArtifactsToDirectory(
        TestCaseResult[] results,
        string directory,
        Encoding encoding,
        bool writeEverything,
        bool deleteDirectory = true)
    {
        // Create directories for everything
        var root = $"{directory}/LuaDecompilerTestResults";
        var compileFailures = $"{root}/CompileFailures";
        var decompileFailures = $"{root}/DecompileFailures";
        var recompileFailures = $"{root}/RecompileFailures";
        var mismatches = $"{root}/Mismatches";
        var matches = $"{root}/Matches";
        if (Directory.Exists(root) && deleteDirectory)
            Directory.Delete(root, true);
        if (!Directory.Exists(compileFailures))
            Directory.CreateDirectory(compileFailures);
        if (!Directory.Exists(decompileFailures))
            Directory.CreateDirectory(decompileFailures);
        if (!Directory.Exists(recompileFailures))
            Directory.CreateDirectory(recompileFailures);
        if (!Directory.Exists(mismatches))
            Directory.CreateDirectory(mismatches);
        if (!Directory.Exists(matches))
            Directory.CreateDirectory(matches);

        foreach (var result in results)
        {
            var outputDirectory = result.Error switch
            {
                TestCaseError.ErrorCompilationFailed => compileFailures,
                TestCaseError.ErrorDecompilationFailed => decompileFailures,
                TestCaseError.ErrorRecompilationFailed => recompileFailures,
                TestCaseError.ErrorMismatch => mismatches,
                TestCaseError.Success => matches,
                _ => throw new Exception("Invalid error")
            };
            if (Path.GetDirectoryName(result.Name) is { } directoryName)
            {
                Directory.CreateDirectory($"{outputDirectory}/{directoryName}");
            }

            var basePath = $"{outputDirectory}/{result.Name}";

            bool shouldDump = writeEverything || result.Error != TestCaseError.Success;
            if (shouldDump && result.Source is { } source)
            {
                File.WriteAllText(basePath, source, encoding);
            }
            
            if (shouldDump && result.CompiledBytes is { } bytes)
            {
                File.WriteAllBytes($"{basePath}.compiled", bytes);
            }
            
            if (shouldDump && result.DecompilationResult is { } decompilationResult)
            {
                if (decompilationResult.DecompiledSource is { } decompiledSource)
                {
                    File.WriteAllText($"{basePath}.decompiled.lua", decompiledSource, encoding);
                }
                
                if (decompilationResult.IrResults.Length > 0)
                {
                    var passBuilder = new StringBuilder(1024 * 512 * decompilationResult.IrResults.Length);
                    foreach (var pass in decompilationResult.IrResults)
                    {
                        passBuilder.Append($"-- Begin pass {pass.Pass} --\n");
                        passBuilder.Append(pass.Ir);
                        passBuilder.Append($"-- End pass {pass.Pass} --\n");
                    }
                    File.WriteAllText($"{basePath}.passes.lua", passBuilder.ToString(), encoding);
                }
                        
                if (decompilationResult.DotGraphResults.Length > 0)
                {
                    foreach (var pass in decompilationResult.DotGraphResults)
                    {
                        foreach (var f in pass.FunctionResults)
                        {
                            if (result.MismatchedFunctionIds != null && !result.MismatchedFunctionIds.Contains(f.FunctionId))
                                continue;
                            File.WriteAllText(
                                $"{basePath}.{pass.Pass.Replace('-', '_')}.{f.FunctionId}.dot", 
                                f.DotGraph);
                        }
                    }
                }
            }
            
            if (shouldDump && result.RecompiledBytes is { } recompiledBytes)
            {
                File.WriteAllBytes($"{basePath}.recompiled", recompiledBytes);

                for (var i = 0; i < result.MismatchedFunctionIds?.Length; i++)
                {
                    if (result.MismatchedCompiledDisassembledFunctions?[i] == null ||
                        result.MismatchedRecompiledDisassembledFunctions?[i] == null)
                        continue;
                    var functionId = result.MismatchedFunctionIds[i];
                    File.WriteAllText($"{basePath}.compiled.disassembled.{functionId}.lua", 
                        result.MismatchedCompiledDisassembledFunctions[i]);
                    File.WriteAllText($"{basePath}.recompiled.disassembled.{functionId}.lua", 
                        result.MismatchedRecompiledDisassembledFunctions[i]);
                }
            }
        }
    }

    /// <summary>
    /// Log the test results in a readable way to the console
    /// </summary>
    /// <param name="results"></param>
    public static void ConsoleLogTestResults(TestCaseResult[] results)
    {
        var compileFails = 0;
        var decompileFails = 0;
        var recompileFails = 0;
        var mismatches = 0;
        var matches = 0;
        
        Console.OutputEncoding = Encoding.UTF8;
        Console.ForegroundColor = ConsoleColor.White;

        foreach (var result in results)
        {
            Console.WriteLine(result.Name);
            switch (result.Error)
            {
                case TestCaseError.ErrorCompilationFailed:
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine("    Compilation Failed!");
                    compileFails++;
                    break;
                case TestCaseError.ErrorDecompilationFailed:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("    Decompilation Failed!");
                    decompileFails++;
                    break;
                case TestCaseError.ErrorRecompilationFailed:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("    Recompilation Failed!");
                    recompileFails++;
                    break;
                case TestCaseError.ErrorMismatch:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("    Mismatched ({0}/{1} matched)",
                        result.TotalFunctionIds - result.MismatchedFunctionIds?.Length, result.TotalFunctionIds);
                    var warningIds = result.DecompilationResult?.FunctionsWithWarnings
                        .Aggregate("            Function IDs with warnings: ", (current, i) => current + $" {i}");
                    if (result.DecompilationResult?.FunctionsWithWarnings is { Length: > 0 })
                        Console.WriteLine(warningIds);
                    var functionIds = result.MismatchedFunctionIds?
                        .Aggregate("            Mismatched Function IDs: ", (current, i) => current + $" {i}");
                    Console.WriteLine(functionIds);
                    mismatches++;
                    break;
                case TestCaseError.Success:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("        Matched");
                    matches++;
                    break;
                default:
                    throw new Exception("Invalid error");
            }

            if (result.ErrorMessage != null)
            {
                Console.WriteLine($"    {result.ErrorMessage}");
            }
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
        }
        
        // Write total statistics
        Console.WriteLine();
        Console.WriteLine("Decompilation stats:");
        Console.WriteLine($"Total Lua Files:      {results.Length}");
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine($"Compilation Failed:   {compileFails}");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Decompilation Failed: {decompileFails}");
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"Recompilation Failed: {recompileFails}");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Mismatches:           {mismatches}");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Matches:              {matches}");
    }
}