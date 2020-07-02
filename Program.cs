using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using luadec.Utilities;

namespace luadec
{
    class Program
    {
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding outEncoding = Encoding.UTF8;
            // Super bad arg parser until I decide to use a better libary
            bool writeFile = true;
            string outfilename = null;
            string infilename = null;
            int arg = 0;
            try
            {
                if (args[arg] == "-d")
                {
                    writeFile = false;
                    arg++;
                }
                else if (args[arg] == "-o")
                {
                    outfilename = args[arg + 1];
                    arg += 2;
                }
                infilename = args[arg];
                if (outfilename == null)
                {
                    outfilename = Path.GetFileNameWithoutExtension(infilename) + ".dec.lua";
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Usage: DSLuaDecompiler.exe [options] inputfile.lua\n-o outputfile.lua\n-d Print output in console");
            }

            Console.OutputEncoding = outEncoding;
            using (FileStream stream = File.OpenRead(infilename))
            {
                BinaryReaderEx br = new BinaryReaderEx(false, stream);
                var lua = new LuaFile(br);
                IR.Function main = new IR.Function();
                //LuaDisassembler.DisassembleFunction(lua.MainFunction);
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
                    LuaDisassembler.GenerateIR53(main, lua.MainFunction);
                    outEncoding = Encoding.UTF8;
                }

                if (writeFile)
                {
                    File.WriteAllText(outfilename, main.ToString(), outEncoding);
                }
                else
                {
                    Console.WriteLine(main.ToString());
                }
            }
        }
    }
}
