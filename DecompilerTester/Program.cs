using System.Diagnostics;
using System.Globalization;
using System.Text;
using LuaDecompilerCore;
using LuaDecompilerCore.IR;
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
            BinaryReaderEx br1 = new BinaryReaderEx(false, a);
            BinaryReaderEx br2 = new BinaryReaderEx(false, b);
            LuaFile luaFile1 = new LuaFile(br1);
            LuaFile luaFile2 = new LuaFile(br2);
            int lmismatched = 0;
            int ltotal = 0;
            var lMismatchedFunctionIds = new List<int>();
            bool flag = CompareFunction(luaFile1.MainFunction, luaFile2.MainFunction);
            mismatchedFunctionIds = lMismatchedFunctionIds;
            mismatched = lmismatched;
            total = ltotal;
            return flag;

            bool CompareFunction(LuaFile.Function f1, LuaFile.Function f2)
            {
                bool flag = true;
                if (f1.ChildFunctions.Length != f2.ChildFunctions.Length)
                {
                    lmismatched++;
                    lMismatchedFunctionIds.Add(f2.FunctionID);
                    flag = false;
                }
                else if (!f1.Bytecode.SequenceEqual(f2.Bytecode))
                {
                    lmismatched++;
                    lMismatchedFunctionIds.Add(f2.FunctionID);
                    flag = false;
                }

                for (int index = 0; index < f1.ChildFunctions.Length; ++index)
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
            ref int matches,
            ref int mismatches)
        {
            Encoding encoding = Encoding.UTF8;
            Console.WriteLine("    Decompiling " + Path.GetFileName(name));
            try
            {
                LuaFile luaFile = new LuaFile(new BinaryReaderEx(false, input));
                Function irfun = new Function(luaFile.MainFunction.FunctionID);
                bool flag = false;
                var options = new DecompilationOptions
                {
                    OutputDebugComments = true
                };
                var decompiler = new LuaDecompiler(options);
                if (luaFile.Version == LuaFile.LuaVersion.Lua50)
                {
                    decompiler.DecompileLua50Function(irfun, luaFile.MainFunction);
                    encoding = Encoding.GetEncoding("shift_jis");
                }
                else if (luaFile.Version == LuaFile.LuaVersion.Lua51HKS)
                {
                    flag = true;
                    decompiler.DecompileHksFunction(irfun, luaFile.MainFunction);
                    encoding = Encoding.UTF8;
                }
                else if (luaFile.Version == LuaFile.LuaVersion.Lua53Smash)
                {
                    decompiler.DecompileLua53Function(irfun, luaFile.MainFunction, true);
                    encoding = Encoding.UTF8;
                }

                string tempFileName1 = Path.GetTempFileName();
                File.WriteAllText(tempFileName1, irfun.ToString(), encoding);
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
                        ++matches;
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
            List<string> list1 = Directory.GetFileSystemEntries(args[0], "*.luabnd.dcx")
                .ToList();
            List<string> list2 =
                Directory.GetFileSystemEntries(args[0], "*.hks").ToList();
            int num = 0;
            int fails = 0;
            int compilefails = 0;
            int mismatches = 0;
            int matches = 0;
            if (Directory.Exists("output"))
                Directory.Delete("output", true);
            if (!Directory.Exists("output/mismatches"))
                Directory.CreateDirectory("output/mismatches");
            if (!Directory.Exists("output/miscompiles"))
                Directory.CreateDirectory("output/miscompiles");
            if (!Directory.Exists("output/failures"))
                Directory.CreateDirectory("output/failures");
            foreach (string path in list1)
            {
                string str1 = "output/mismatches/" + Path.GetFileNameWithoutExtension(path);
                string str2 = "output/miscompiles/" + Path.GetFileNameWithoutExtension(path);
                string str3 = "output/failures/" + Path.GetFileNameWithoutExtension(path);
                Console.WriteLine("Decompiling luabnd " + Path.GetFileName(path));
                foreach (SoulsFormats.BinderFile file in SoulsFormats.BND4.Read(path).Files)
                {
                    if (file.Name.EndsWith(".lua"))
                    {
                        ++num;
                        TestLua(file.Name, file.Bytes, str1, str2, str3, ref fails, ref compilefails,
                            ref matches, ref mismatches);
                    }
                }
            }

            foreach (string str4 in list2)
            {
                string str5 = "output/mismatches/action";
                if (!Directory.Exists(str5))
                    Directory.CreateDirectory(str5);
                string str6 = "output/miscompiles/action";
                if (!Directory.Exists(str6))
                    Directory.CreateDirectory(str6);
                string str7 = "output/failures/action";
                if (!Directory.Exists(str7))
                    Directory.CreateDirectory(str7);
                byte[] input = File.ReadAllBytes(str4);
                ++num;
                TestLua(str4, input, str5, str6, str7, ref fails, ref compilefails, ref matches,
                    ref mismatches);
            }

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
            Console.WriteLine($"Matches:              {matches}");
            Console.ReadLine();
        }
    }
}