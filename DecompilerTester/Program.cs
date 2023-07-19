using System.Diagnostics;
using System.Globalization;
using System.Text;
using LuaCompiler;
using LuaCompiler.Compilers;
using LuaDecompilerCore;
using LuaDecompilerCore.IR;
using LuaDecompilerCore.LanguageDecompilers;
using LuaDecompilerCore.Utilities;

namespace DecompilerTester
{
    /// <summary>
    /// Code is weird because original source code to this was lost and it needed to be decompiled from a build lying around
    /// </summary>
    internal static class Program
    {
        public static bool AreFileContentsEqual(string path1, byte[] bytes) =>
            File.ReadAllBytes(path1).SequenceEqual(bytes);

        private static bool CompareLuaFiles(byte[] a, byte[] b, 
            out int mismatched, out int total, out List<int> mismatchedFunctionIds)
        {
            var br1 = new BinaryReaderEx(false, a);
            var br2 = new BinaryReaderEx(false, b);
            var luaFile1 = new LuaFile(br1);
            var luaFile2 = new LuaFile(br2);
            var lmismatched = 0;
            var ltotal = 0;
            var lMismatchedFunctionIds = new List<int>();
            var flag = CompareFunction(luaFile1.MainFunction, luaFile2.MainFunction);
            mismatchedFunctionIds = lMismatchedFunctionIds;
            mismatched = lmismatched;
            total = ltotal;
            return flag;

            bool CompareFunction(LuaFile.Function f1, LuaFile.Function f2)
            {
                var flag = true;
                if (f1.ChildFunctions.Length != f2.ChildFunctions.Length)
                {
                    lmismatched++;
                    lMismatchedFunctionIds.Add(f2.FunctionId);
                    flag = false;
                }
                else if (!f1.Bytecode.SequenceEqual(f2.Bytecode))
                {
                    lmismatched++;
                    lMismatchedFunctionIds.Add(f2.FunctionId);
                    flag = false;
                }

                for (var index = 0; index < f1.ChildFunctions.Length; ++index)
                {
                    if (!CompareFunction(f1.ChildFunctions[index], f2.ChildFunctions[index]))
                        flag = false;
                }

                ltotal++;
                return flag;
            }
        }

        private static void TestLua(
            string name,
            byte[] input,
            string dir,
            string dirmiss,
            string dirfail,
            ref int fails,
            ref int compilefails,
            ref List<string> matches,
            ref int mismatches)
        {
            Console.WriteLine("    Decompiling " + Path.GetFileName(name));
            try
            {
                var luaFile = new LuaFile(new BinaryReaderEx(false, input));
                var irFunction = new Function(luaFile.MainFunction.FunctionId);
                var options = new DecompilationOptions
                {
                    OutputDebugComments = true
                };
                
                var encoding = luaFile.Version == LuaFile.LuaVersion.Lua50 ? 
                    Encoding.GetEncoding("shift_jis") : Encoding.UTF8;
                var decompiler = new LuaDecompiler(options);
                ILanguageDecompiler languageDecompiler = luaFile.Version switch
                {
                    LuaFile.LuaVersion.Lua50 => new Lua50Decompiler(),
                    LuaFile.LuaVersion.Lua51Hks => new HksDecompiler(),
                    LuaFile.LuaVersion.Lua53Smash => new Lua53Decompiler(),
                    _ => throw new Exception()
                };
                decompiler.DecompileLuaFunction(languageDecompiler, irFunction, luaFile.MainFunction);

#if false
                string tempFileName1 = Path.GetTempFileName();
                var printer = new FunctionPrinter();
                File.WriteAllText(tempFileName1,  printer.PrintFunction(irfun), encoding);
                string tempFileName2 = Path.GetTempFileName();
                Process process = new Process();
                process.StartInfo.FileName = flag ? "hksc.exe" : "luac-5.0.2.exe";
                process.StartInfo.Arguments = "-s -o " + tempFileName2 + " " + tempFileName1;
                process.Start();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("        Recompilation Failed!");
                    Console.ForegroundColor = ConsoleColor.White;
                    ++compilefails;
                    if (!Directory.Exists(dirmiss))
                        Directory.CreateDirectory(dirmiss);
                    File.WriteAllBytes(dirmiss + "\\" + Path.GetFileName(name), input);
                    File.Copy(tempFileName1, dirmiss + "\\" + Path.GetFileName(name) + ".decomp");
                }
                else
                {
                    if (CompareLuaFiles(File.ReadAllBytes(tempFileName2), 
                            input, 
                            out var mismatched, 
                            out var total, 
                            out var mismatchedFunctionIds))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("        Matched");
                        Console.ForegroundColor = ConsoleColor.White;
                        matches.Add(Path.GetFileName(name));
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("        Mismatched ({0}/{1} matched)", total - mismatched, total);
                        var functionIds = mismatchedFunctionIds.Aggregate("            Mismatched Function IDs: ", 
                            (current, i) => current + $" {i}");
                        Console.WriteLine(functionIds);
                        Console.ForegroundColor = ConsoleColor.White;
                        ++mismatches;
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        File.WriteAllBytes(dir + "\\" + Path.GetFileName(name), input);
                        File.Copy(tempFileName1, dir + "\\" + Path.GetFileName(name) + ".decomp");
                        File.Copy(tempFileName2, dir + "\\" + Path.GetFileName(name) + ".recomp");
                    }
                }
#endif
                
                // Print decompiled output to string
                var printer = new FunctionPrinter();
                var decompiledSource = printer.PrintFunction(irFunction);

                // Attempt to recompile the output
                try
                {
                    ICompiler compiler = luaFile.Version switch
                    {
                        LuaFile.LuaVersion.Lua50 => new Lua50Compiler(),
                        LuaFile.LuaVersion.Lua51Hks => new LuaHavokScriptCompiler(),
                        LuaFile.LuaVersion.Lua53Smash => throw new Exception("Lua 5.3 not supported yet"),
                        _ => throw new Exception()
                    };
                    var compiledBytes = compiler.CompileSource(decompiledSource, encoding);
                    
                    if (CompareLuaFiles(compiledBytes, 
                            input, 
                            out var mismatched, 
                            out var total, 
                            out var mismatchedFunctionIds))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("        Matched");
                        Console.ForegroundColor = ConsoleColor.White;
                        matches.Add(Path.GetFileName(name));
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("        Mismatched ({0}/{1} matched)", total - mismatched, total);
                        var functionIds = mismatchedFunctionIds.Aggregate("            Mismatched Function IDs: ", 
                            (current, i) => current + $" {i}");
                        Console.WriteLine(functionIds);
                        Console.ForegroundColor = ConsoleColor.White;
                        ++mismatches;
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        File.WriteAllBytes(dir + "\\" + Path.GetFileName(name), input);
                        File.WriteAllText(dir + "\\" + Path.GetFileName(name) + ".decomp", decompiledSource);
                        File.WriteAllBytes(dir + "\\" + Path.GetFileName(name) + ".recomp", compiledBytes);
                    }
                }
                // Recompilation failed
                catch (CompileException e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("        Recompilation Failed!");
                    Console.WriteLine(e.Message);
                    Console.ForegroundColor = ConsoleColor.White;
                    ++compilefails;
                    if (!Directory.Exists(dirmiss))
                        Directory.CreateDirectory(dirmiss);
                    File.WriteAllBytes(dirmiss + "\\" + Path.GetFileName(name), input);
                    File.WriteAllText(dirmiss + "\\" + Path.GetFileName(name) + ".decomp", decompiledSource);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("        Failed to decompile " + Path.GetFileName(name));
                Console.ForegroundColor = ConsoleColor.White;
                ++fails;
                if (!Directory.Exists(dirfail))
                    Directory.CreateDirectory(dirfail);
                File.WriteAllBytes(dirfail + "\\" + Path.GetFileName(name), input);
            }
        }

        private static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Console.OutputEncoding = Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.White;
            var list1 = Directory.GetFileSystemEntries(args[0], "*.luabnd.dcx").ToList();
            var list2 = Directory.GetFileSystemEntries(args[0], "*.hks").ToList();
            var num = 0;
            var fails = 0;
            var compilefails = 0;
            var mismatches = 0;
            List<string> matches = new();
            if (Directory.Exists("output"))
                Directory.Delete("output", true);
            if (!Directory.Exists("output/mismatches"))
                Directory.CreateDirectory("output/mismatches");
            if (!Directory.Exists("output/miscompiles"))
                Directory.CreateDirectory("output/miscompiles");
            if (!Directory.Exists("output/failures"))
                Directory.CreateDirectory("output/failures");
            foreach (var path in list1)
            {
                var str1 = "output/mismatches/" + Path.GetFileNameWithoutExtension(path);
                var str2 = "output/miscompiles/" + Path.GetFileNameWithoutExtension(path);
                var str3 = "output/failures/" + Path.GetFileNameWithoutExtension(path);
                Console.WriteLine("Decompiling luabnd " + Path.GetFileName(path));
                foreach (var file in SoulsFormats.BND4.Read(path).Files)
                {
                    if (file.Name.EndsWith(".lua"))
                    {
                        ++num;
                        TestLua(file.Name, file.Bytes, str1, str2, str3, ref fails, ref compilefails,
                            ref matches, ref mismatches);
                    }
                }
            }

            foreach (var str4 in list2)
            {
                var str5 = "output/mismatches/action";
                if (!Directory.Exists(str5))
                    Directory.CreateDirectory(str5);
                var str6 = "output/miscompiles/action";
                if (!Directory.Exists(str6))
                    Directory.CreateDirectory(str6);
                var str7 = "output/failures/action";
                if (!Directory.Exists(str7))
                    Directory.CreateDirectory(str7);
                var input = File.ReadAllBytes(str4);
                ++num;
                TestLua(str4, input, str5, str6, str7, ref fails, ref compilefails, ref matches,
                    ref mismatches);
            }

            var matchesDir = "output/matches.txt";
            File.WriteAllLines(matchesDir, matches);

            Console.WriteLine();
            Console.WriteLine("Decompilation stats:");
            Console.WriteLine($"Total Lua Files:      {num}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Decompilation Failed: {fails}");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"Recompilation Failed: {compilefails}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Mismatches:           {mismatches}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Matches:              {matches.Count}");
            Console.ReadLine();
        }
    }
}