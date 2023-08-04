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
        public int DefiningInstructionBlock;
        public int DefiningInstructionIndex;
        public int UseCount;

        public IdentifierInfo()
        {
            UseCount = 0;
            DefiningInstructionBlock = -1;
            DefiningInstructionIndex = -1;
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
        for (var b = 0; b < function.BlockList.Count; b++)
        {
            var block = function.BlockList[b];
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
            
            for (var i = 0; i < block.Instructions.Count; i++)
            {
                var instruction = block.Instructions[i];
                definesSet.Clear();
                foreach (var def in instruction.GetDefines(definesSet, true))
                {
                    GetIdentifierInfo(def).DefiningInstruction = instruction;
                    GetIdentifierInfo(def).DefiningInstructionBlock = b;
                    GetIdentifierInfo(def).DefiningInstructionIndex = i;
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
    
    public int DefiningInstructionBlock(Identifier identifier)
    {
        if (_identifierInfos == null)
            throw new Exception("Analysis not run");
        
        if (!identifier.IsRenamedRegister)
            return -1;
        
        return GetIdentifierInfo(identifier).DefiningInstructionBlock;
    }
    
    public int DefiningInstructionIndex(Identifier identifier)
    {
        if (_identifierInfos == null)
            throw new Exception("Analysis not run");
        
        if (!identifier.IsRenamedRegister)
            return -1;
        
        return GetIdentifierInfo(identifier).DefiningInstructionIndex;
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