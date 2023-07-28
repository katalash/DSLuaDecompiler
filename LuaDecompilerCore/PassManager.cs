using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuaDecompilerCore.Analyzers;
using LuaDecompilerCore.IR;
using LuaDecompilerCore.Passes;

namespace LuaDecompilerCore;

/// <summary>
/// Stores contextual information of functions in between passes
/// </summary>
public class FunctionContext : IDisposable
{
    private readonly DecompilationContext _context;
    private readonly Function _function;
    private readonly List<IAnalyzer> _analyzers = new List<IAnalyzer>(5);

    public FunctionContext(DecompilationContext context, Function function)
    {
        _context = context;
        _function = function;
    }
    
    /// <summary>
    /// Gets an analyzer analysis. If one already exists the cached analysis will be returned. Otherwise a new one
    /// will be created and ran.
    /// </summary>
    /// <typeparam name="T">The analyzer to run</typeparam>
    /// <returns>The result of the analyzer</returns>
    public T GetAnalysis<T>() where T : IAnalyzer, new()
    {
        foreach (var analyzer in _analyzers)
        {
            if (analyzer is T a)
                return a;
        }

        var newAnalyzer = new T();
        newAnalyzer.Run(_context, this, _function);
        _analyzers.Add(newAnalyzer);
        return newAnalyzer;
    }

    /// <summary>
    /// Invalidates a cached analysis. Should be used if modifications occur such that the analysis is no longer
    /// valid.
    /// </summary>
    /// <typeparam name="T">The analyzer to invalidate</typeparam>
    public void InvalidateAnalysis<T>()
    {
        for (var i = 0; i < _analyzers.Count; i++)
        {
            if (_analyzers[i] is not T) continue;
            _analyzers[i].Dispose();
            _analyzers.RemoveAt(i);
            return;
        }
    }

    public void Dispose()
    {
        foreach (var analyzer in _analyzers)
        {
            analyzer.Dispose();
        }
    }
}

/// <summary>
/// Pass manager to run all the passes on functions and allow debugging of passes.
/// </summary>
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
        var irResults = new List<PassIrResult>(_passes.Count);
        var dotGraphResults = new List<PassDotGraphResult>(_passes.Count);
        var functionContexts = ArrayPool<FunctionContext>.Shared.Rent(functions.Count);

        for (var i = 0; i < functions.Count; i++)
        {
            functionContexts[i] = new FunctionContext(context, functions[i]);
        }
        
        var printer = new FunctionPrinter();
        string? error = null;
        foreach (var pass in _passes)
        {
            for (var i = 0; i < functions.Count; i++)
            {
                if (catchExceptions)
                {
                    try
                    {
                        pass.Pass.RunOnFunction(context, functionContexts[i], functions[i]);
                    }
                    catch (Exception e)
                    {
                        error = e.Message + "\n\n" + e.StackTrace;
                        goto Error;
                    }
                }
                else
                {
                    pass.Pass.RunOnFunction(context, functionContexts[i], functions[i]);
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
        List<int> functionsWithWarnings = new List<int>(functions.Count);
        for (var i = 0; i < functions.Count; i++)
        {
            if (functions[i].Warnings.Count > 0)
                functionsWithWarnings.Add(i);
            functionContexts[i].Dispose();
        }
        ArrayPool<FunctionContext>.Shared.Return(functionContexts);
        return new DecompilationResult(
             error == null ? printer.PrintFunction(functions[0]) : null, 
             error, 
             irResults.ToArray(),
             dotGraphResults.ToArray(),
             functionsWithWarnings.ToArray());
    }
}