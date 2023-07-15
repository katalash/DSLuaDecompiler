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

    public PassManager(DecompilationOptions options)
    {
        _passes = new List<PassRegistration>();
        _dumpIrPasses = options.DumpIrPasses;
        _dumpAllIrPasses = _dumpIrPasses.Contains("all");
    }
    
    public PassManager(List<PassRegistration> passes, DecompilationOptions options)
    {
        _passes = passes;
        _dumpIrPasses = options.DumpIrPasses;
        _dumpAllIrPasses = _dumpIrPasses.Contains("all");
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
    /// <returns>String representation of the first function after each specified pass runs</returns>
    public string RunOnFunctions(DecompilationContext context, IReadOnlyList<Function> functions)
    {
        var printPassCount = _dumpAllIrPasses ? _passes.Count : _dumpIrPasses.Count;
        var output = new StringBuilder(printPassCount * 1024 * 1024);
        var printer = new FunctionPrinter();
        foreach (var pass in _passes)
        {
            foreach (var f in functions)
            {
                pass.Pass.RunOnFunction(context, f);
            }
            
            // Skip dumping if the pass isn't enabled
            if (!_dumpAllIrPasses && !_dumpIrPasses.Contains(pass.Name)) continue;

            output.AppendLine($"-- Begin pass {pass.Name} --");
            printer.PrintFunctionToStringBuilder(functions[0], output);
            output.AppendLine($"-- End pass {pass.Name} --");
            output.AppendLine();
        }

        return output.Length > 0 ? output.ToString() : null;
    }
}