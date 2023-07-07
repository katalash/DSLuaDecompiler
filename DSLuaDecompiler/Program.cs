using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text;
using LuaDecompilerCore;
using LuaDecompilerCore.IR;
using LuaDecompilerCore.Utilities;

namespace DSLuaDecompiler
{
    internal static class Program
    {
        internal class DecompilationOptionsBinder : BinderBase<DecompilationOptions>
        {
            private readonly Option<int[]?> _includedFunctionIds;
            private readonly Option<int[]> _excludedFunctionIds;
            private readonly Option<string[]> _dumpIrPasses;
            private readonly Option<bool> _debugComments;
            
            public DecompilationOptionsBinder(
                Option<int[]?> includedFunctionIds, 
                Option<int[]> excludedFunctionIds, 
                Option<string[]> dumpIrPasses, 
                Option<bool> debugComments)
            {
                _includedFunctionIds = includedFunctionIds;
                _excludedFunctionIds = excludedFunctionIds;
                _dumpIrPasses = dumpIrPasses;
                _debugComments = debugComments;
            }

            protected override DecompilationOptions GetBoundValue(BindingContext bindingContext) => 
                new()
                {
                    IncludedFunctionIds = bindingContext.ParseResult.HasOption(_includedFunctionIds) ? 
                        bindingContext.ParseResult.GetValueForOption(_includedFunctionIds)?.ToHashSet() : null,
                    ExcludedFunctionIds = bindingContext.ParseResult.GetValueForOption(_excludedFunctionIds)?.ToHashSet() ?? throw new InvalidOperationException(),
                    DumpIrPasses = bindingContext.ParseResult.GetValueForOption(_dumpIrPasses)?.ToHashSet() ?? throw new InvalidOperationException(),
                    OutputDebugComments = bindingContext.ParseResult.GetValueForOption(_debugComments)
                };
        }

        private static int Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Core options
            var fileArgument = new Argument<string>("file", "The path to the Lua file to decompile.");
            var outputOption = new Option<string?>("--output", "The path to write the decompiled output.");
            outputOption.AddAlias("-o");
            var consoleOption = new Option<bool>("--console", "Print output to console instead of file");
            consoleOption.AddAlias("-c");
            
            // Debug options
            var includedFunctionIdsOption = new Option<int[]?>(
                name: "--included-function-ids",
                description: "If specified, only the listed function IDs will be processed.")
            {
                Arity = ArgumentArity.OneOrMore,
                AllowMultipleArgumentsPerToken = true
            };
            var excludedFunctionIdsOption = new Option<int[]>(
                name: "--excluded-function-ids",
                description: "If specified, the listed function IDs will be explicitly not processed.")
                {
                    AllowMultipleArgumentsPerToken = true
                };
            var dumpIrPassesOption = new Option<string[]>(
                name: "--print-ir-passes",
                description: "List of intermediate passes to print the IR after for debugging.")
                {
                    AllowMultipleArgumentsPerToken = true
                };
            var insertDebugComments = new Option<bool>(
                name: "--insert-debug-comments",
                description: "If enabled, debug comments will be added in the decompiled lua source.");
            
            var rootCommand = new RootCommand("Dark Souls Lua Decompiler")
            {
                fileArgument, 
                outputOption, 
                consoleOption, 
                includedFunctionIdsOption, 
                excludedFunctionIdsOption, 
                dumpIrPassesOption, 
                insertDebugComments
            };
            rootCommand.SetHandler((file, output, console, decompilationOptions) =>
            {
                var decompiler = new LuaDecompiler(decompilationOptions);
                
                Encoding outEncoding = Encoding.UTF8;
                Console.OutputEncoding = outEncoding;
                using var stream = File.OpenRead(file);
                var br = new BinaryReaderEx(false, stream);
                var lua = new LuaFile(br);
                var main = new Function(lua.MainFunction.FunctionID);
                if (lua.Version == LuaFile.LuaVersion.Lua50)
                {
                    decompiler.DecompileLua50Function(main, lua.MainFunction);
                    outEncoding = Encoding.GetEncoding("shift_jis");
                }
                else if (lua.Version == LuaFile.LuaVersion.Lua51HKS)
                {
                    decompiler.DecompileHksFunction(main, lua.MainFunction);
                    outEncoding = Encoding.UTF8;
                }
                else if (lua.Version == LuaFile.LuaVersion.Lua53Smash)
                {
                    decompiler.DecompileLua53Function(main, lua.MainFunction, true);
                    outEncoding = Encoding.UTF8;
                }

                var outputFile = output ?? Path.GetFileNameWithoutExtension(file) + ".dec.lua";
                if (!console)
                {
                    File.WriteAllText(outputFile, main.ToString(), outEncoding);
                }
                else
                {
                    Console.WriteLine(main.ToString());
                }
            }, fileArgument, 
                outputOption, 
                consoleOption, 
                new DecompilationOptionsBinder(includedFunctionIdsOption, 
                    excludedFunctionIdsOption, 
                    dumpIrPassesOption, 
                    insertDebugComments));

            return rootCommand.Invoke(args);
        }
    }
}
