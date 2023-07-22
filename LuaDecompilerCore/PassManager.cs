using System;
using System.Collections.Generic;
using System.Text;
using LuaDecompilerCore.IR;
using LuaDecompilerCore.Passes;

namespace LuaDecompilerCore;

public class PassManager
{
    public record struct PassRegistration(string Name, IPass Pass);

    private readonly List<PassRegistration> _passes;
    private readonly IReadOnlySet<string> _dumpIrPasses;
    private readonly bool _dumpAllIrPasses;
    private readonly IReadOnlySet<string> _dumpDotGraphPasses;
    private readonly bool _dumpAllDotGraphPasses;
    private readonly bool _dumpCfgMutatingDotGraphPasses;

    public PassManager(DecompilationOptions options)
    {
        _passes = new List<PassRegistration>();
        _dumpIrPasses = options.DumpIrPasses;
        _dumpAllIrPasses = _dumpIrPasses.Contains("all");
        _dumpDotGraphPasses = options.DumpDotGraphPasses;
        _dumpAllDotGraphPasses = _dumpDotGraphPasses.Contains("all");
        _dumpCfgMutatingDotGraphPasses = _dumpDotGraphPasses.Contains("cfg-mutated");
    }

    public void AddPass(string name, IPass pass)
    {
        _passes.Add(new PassRegistration(name, pass));
    }

    /// <summary>
    /// Run the registered passes on all the functions, printing out the root function after the specified passes
    /// execute. This runs a pass on all the functions before proceeding to the next pass.
    /// </summary>
    /// <param name="context">The decompilation context provided to every pass</param>
    /// <param name="functions">The functions to run all the passes on</param>
    /// <param name="catchExceptions"></param>
    /// <returns>String representation of the first function after each specified pass runs</returns>
    public DecompilationResult RunOnFunctions(DecompilationContext context,
        IReadOnlyList<Function> functions,
        bool catchExceptions)
    {
        var irResults = new List<PassIrResult>();
        var dotGraphResults = new List<PassDotGraphResult>();
        
        var printer = new FunctionPrinter();
        string? error = null;
        foreach (var pass in _passes)
        {
            foreach (var f in functions)
            {
                if (catchExceptions)
                {
                    try
                    {
                        pass.Pass.RunOnFunction(context, f);
                    }
                    catch (Exception e)
                    {
                        error = e.Message + "\n\n" + e.StackTrace;
                        goto Error;
                    }
                }
                else
                {
                    pass.Pass.RunOnFunction(context, f);
                }
            }
            
            // Dump pass IR
            if (_dumpAllIrPasses || _dumpIrPasses.Contains(pass.Name))
            {
                var output = new StringBuilder(128 * 1024);
                printer.PrintFunctionToStringBuilder(functions[0], output);
                irResults.Add(new PassIrResult(pass.Name, output.ToString()));
            }
            
            // Dump dot files
            if (_dumpAllDotGraphPasses || 
                (_dumpCfgMutatingDotGraphPasses && pass.Pass.MutatesCfg) || 
                _dumpDotGraphPasses.Contains(pass.Name))
            {
                var functionDotResults = new List<FunctionDotGraphResult>();
                foreach (var f in functions)
                {
                    functionDotResults.Add(
                        new FunctionDotGraphResult(f.FunctionId, printer.PrintDotGraphForFunction(f)));
                }
                dotGraphResults.Add(new PassDotGraphResult(pass.Name, functionDotResults.ToArray()));
            }
        }
        
        Error:
        return new DecompilationResult(
             error == null ? printer.PrintFunction(functions[0]) : null, 
             error, 
             irResults.ToArray(),
             dotGraphResults.ToArray());
    }
}