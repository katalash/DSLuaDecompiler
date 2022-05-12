using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Mime;
using System.Threading.Channels;
using luadec;
using luadec.Utilities;

namespace luadec
{
    class Program
    {
        public static void UsageStatement()
        {
            Console.WriteLine("Usage: DSLuaDecompiler.exe [options] inputfile.lua\n-o outputfile.lua\n-d Print output in console");
            Environment.Exit(0);
        }
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Console.OutputEncoding = Encoding.UTF8;
            // Super bad arg parser until I decide to use a better libary
            bool writeFile = true;
            string outfilename = null;
            string infilename = null;
            int arg = 0;
            while (arg < args.Length)
            {
                try
                {
                    if (args[arg].ToLower() == "-d")
                    {
                        writeFile = false;
                        arg++;
                        continue;
                    }
                    if (args[arg].ToLower() == "-o")
                    {
                        outfilename = args[arg + 1];
                        arg += 2;
                        continue;
                    }
                    if (args[arg].ToLower() == "-h")
                    {
                        outfilename = args[arg + 1];
                        arg += 2;
                        continue;
                    }

                    infilename = args[arg];
                    if (!File.Exists(infilename))
                        UsageStatement();

                    if (outfilename == null)
                    {
                        outfilename =
                            Path.GetDirectoryName(infilename) + "/decompiled/" +
                            Path.GetFileNameWithoutExtension(infilename) + ".dec.lua";
                    }
                }
                catch (Exception)
                {
                    UsageStatement();
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outfilename));

#if DEBUG
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Decompile(infilename, outfilename, writeFile);
                }
                else
                {
#endif
                    try
                    {
                        Decompile(infilename, outfilename, writeFile);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Exception while decompiling {infilename}:");
                        Console.WriteLine(e.Message);
                        Console.WriteLine(e.StackTrace);
                        Console.WriteLine("");
                    }
#if DEBUG
                }
#endif

                outfilename = null;
                arg++;
            }
        }

        private static void Decompile(string infilename, string outfilename, bool writeFile)
        {
            using (FileStream stream = File.OpenRead(infilename))
            {
                BinaryReaderEx br = new BinaryReaderEx(false, stream);
                var lua = new LuaFile(br);
                IR.Function main = new IR.Function();
                //LuaDisassembler.DisassembleFunction(lua.MainFunction);
                Encoding outEncoding = Encoding.UTF8;
                if (lua.Version == LuaFile.LuaVersion.Lua50)
                {
                    LuaDisassembler.GenerateIR50(main, lua.MainFunction);
                    outEncoding = Encoding.GetEncoding("shift_jis");
                }
                else if (lua.Version == LuaFile.LuaVersion.Lua51HKS)
                {
                    LuaDisassembler.GenerateIRHKS(main, lua.MainFunction);
                    outEncoding = Encoding.UTF8;
                }
                else if (lua.Version == LuaFile.LuaVersion.Lua53Smash)
                {
                    LuaDisassembler.GenerateIR53(main, lua.MainFunction, true);
                    outEncoding = Encoding.UTF8;
                }

                if (writeFile)
                {
                    File.WriteAllText(outfilename, main.ToString(), outEncoding);
                }
                else
                {
                    Console.OutputEncoding = outEncoding;
                    Console.WriteLine(main.ToString());
                }
            }
        }
    }
}
