using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using LuaDecompilerCore.IR;
using LuaDecompilerCore.Utilities;

namespace LuaDecompilerCore.Analyzers;

/// <summary>
/// Analyzer that analyzes the definitions and use counts of all the identifiers. 
/// </summary>
public class IdentifierDefinitionUseAnalyzer : IAnalyzer
{
    private struct IdentifierInfo
    {
        public Instruction? DefiningInstruction;
        public int UseCount;

        public IdentifierInfo()
        {
            UseCount = 0;
        }
    }

    private JaggedArray<IdentifierInfo>? _identifierInfos;

    private ref IdentifierInfo GetIdentifierInfo(Identifier identifier)
    {
        Debug.Assert(_identifierInfos != null);
        return ref _identifierInfos.Value[(int)identifier.RegNum][(int)identifier.RegSubscriptNum];
    }

    public void Run(DecompilationContext decompilationContext, FunctionContext functionContext, Function function)
    {
        if (function.RenamedRegisterCounts == null)
        {
            throw new Exception("Analysis requires SSA form");
        }
        
        _identifierInfos = new JaggedArray<IdentifierInfo>(function.RenamedRegisterCounts, true);

        var definesSet = new HashSet<Identifier>(2);
        var usesSet = new HashSet<Identifier>(10);
        foreach (var block in function.BlockList)
        {
            foreach (var phi in block.PhiFunctions)
            {
                definesSet.Clear();
                foreach (var def in phi.Value.GetDefines(definesSet, true))
                {
                    GetIdentifierInfo(def).DefiningInstruction = phi.Value;
                }
                
                usesSet.Clear();
                foreach (var use in phi.Value.GetUses(definesSet, true))
                {
                    GetIdentifierInfo(use).UseCount++;
                }
            }
            
            foreach (var instruction in block.Instructions)
            {
                definesSet.Clear();
                foreach (var def in instruction.GetDefines(definesSet, true))
                {
                    GetIdentifierInfo(def).DefiningInstruction = instruction;
                }
                
                usesSet.Clear();
                foreach (var use in instruction.GetUses(usesSet, true))
                {
                    if (!definesSet.Contains(use))
                        GetIdentifierInfo(use).UseCount += instruction.UseCount(use);
                }
            }
        }
    }

    public Instruction? DefiningInstruction(Identifier identifier)
    {
        if (_identifierInfos == null)
            throw new Exception("Analysis not run");
        
        if (!identifier.IsRenamedRegister)
            return null;
        
        return GetIdentifierInfo(identifier).DefiningInstruction;
    }

    public int UseCount(Identifier identifier)
    {
        if (_identifierInfos == null)
            throw new Exception("Analysis not run");
        
        if (!identifier.IsRenamedRegister)
            return 0;
        
        return GetIdentifierInfo(identifier).UseCount;
    }

    public void Dispose()
    {
        _identifierInfos?.Dispose();
    }
}