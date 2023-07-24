using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using LuaDecompilerCore.IR;

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
    
    private uint[]? _renamedRegisterOffsets;
    private IdentifierInfo[]? _identifierInfos;

    private int IdentifierIndex(Identifier identifier)
    {
        Debug.Assert(_renamedRegisterOffsets != null);
        return (int)_renamedRegisterOffsets[(int)identifier.RegNum] + (int)identifier.RegSubscriptNum;
    }
    
    private void SetDefineInstruction(Identifier identifier, Instruction instruction)
    {
        Debug.Assert(_identifierInfos != null);
        _identifierInfos[IdentifierIndex(identifier)].DefiningInstruction = instruction;
    }
    
    public void Run(DecompilationContext decompilationContext, FunctionContext functionContext, Function function)
    {
        if (function.RenamedRegisterCounts == null)
        {
            throw new Exception("Analysis requires SSA form");
        }

        uint totalSum = 0;
        _renamedRegisterOffsets = ArrayPool<uint>.Shared.Rent(function.RenamedRegisterCounts.Length);
        for (var i = 0; i < function.RegisterCount; i++)
        {
            _renamedRegisterOffsets[i] = totalSum;
            totalSum += function.RenamedRegisterCounts[i];
        }

        _identifierInfos = ArrayPool<IdentifierInfo>.Shared.Rent((int)totalSum);
        for (var i = 0; i < totalSum; i++)
        {
            _identifierInfos[i] = new IdentifierInfo();
        }

        var definesSet = new HashSet<Identifier>(2);
        var usesSet = new HashSet<Identifier>(10);
        foreach (var block in function.BlockList)
        {
            foreach (var phi in block.PhiFunctions)
            {
                definesSet.Clear();
                foreach (var def in phi.Value.GetDefines(definesSet, true))
                {
                    SetDefineInstruction(def, phi.Value);
                }
                
                usesSet.Clear();
                foreach (var use in phi.Value.GetUses(definesSet, true))
                {
                    _identifierInfos[IdentifierIndex(use)].UseCount++;
                }
            }
            
            foreach (var instruction in block.Instructions)
            {
                definesSet.Clear();
                foreach (var def in instruction.GetDefines(definesSet, true))
                {
                    SetDefineInstruction(def, instruction);
                }
                
                usesSet.Clear();
                foreach (var use in instruction.GetUses(usesSet, true))
                {
                    if (!definesSet.Contains(use))
                        _identifierInfos[IdentifierIndex(use)].UseCount++;
                }
            }
        }
    }

    public Instruction? DefiningInstruction(Identifier identifier)
    {
        if (_renamedRegisterOffsets == null || _identifierInfos == null)
            throw new Exception("Analysis not run");
        
        if (!identifier.IsRenamedRegister)
            return null;
        
        return _identifierInfos[IdentifierIndex(identifier)].DefiningInstruction;
    }

    public int UseCount(Identifier identifier)
    {
        if (_renamedRegisterOffsets == null || _identifierInfos == null)
            throw new Exception("Analysis not run");
        
        if (!identifier.IsRenamedRegister)
            return 0;
        
        return _identifierInfos[IdentifierIndex(identifier)].UseCount;
    }

    public void Dispose()
    {
        if (_renamedRegisterOffsets != null)
            ArrayPool<uint>.Shared.Return(_renamedRegisterOffsets);
        if (_identifierInfos != null)
            ArrayPool<IdentifierInfo>.Shared.Return(_identifierInfos);
    }
}