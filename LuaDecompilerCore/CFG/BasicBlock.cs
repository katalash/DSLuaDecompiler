using System;
using System.Collections.Generic;
using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.CFG
{
    /// <summary>
    /// A block of instructions in which control flow only enters at the beginning and leaves at the end
    /// </summary>
    public sealed class BasicBlock
    {
        public readonly int BlockId;
        public List<BasicBlock> Predecessors;
        public List<BasicBlock> Successors;
        public List<Instruction> Instructions;
        public Dictionary<Identifier, PhiFunction> PhiFunctions;

        public readonly HashSet<Identifier> PhiMerged = new();

        /// <summary>
        /// Set of basic blocks that dominate this function
        /// </summary>
        public HashSet<BasicBlock> Dominance;

        /// <summary>
        /// The closest node that dominates this block
        /// </summary>
        public BasicBlock ImmediateDominator;

        /// <summary>
        /// Blocks that have this block as their immediate dominator
        /// </summary>
        public List<BasicBlock> DominanceTreeSuccessors;

        // Live analysis stuff
        public HashSet<Identifier> UpwardExposedIdentifiers;
        public HashSet<Identifier> KilledIdentifiers;
        public HashSet<Identifier> LiveOut;

        /// <summary>
        /// Register IDs of registers killed (i.e. redefined) under the scope of this block (excluding this block)
        /// </summary>
        public HashSet<uint> ScopeKilled = new();

        // Control flow analysis
        public int ReversePostorderNumber = 0;
        public int OrderNumber = 0;
        public bool IsLoopHead = false;
        public bool IsLoopLatch = false;
        public BasicBlock? LoopFollow = null;
        public readonly List<BasicBlock> LoopLatches = new();
        public LoopType LoopType = LoopType.LoopNone;
        public BasicBlock? Follow = null;
        public BasicBlock? LoopBreakFollow = null;
        public BasicBlock? LoopContinueFollow = null;
        public bool IsBreakNode = false;
        public bool IsContinueNode = false;
        // Set to true if both the true and false branch lead to a return
        public bool IfOrphaned = false;

        // Code gen
        public bool IsInfiniteLoop = false;
        private bool _isCodeGenerated;

        /// <summary>
        /// Used for SSA construction
        /// </summary>
        public HashSet<BasicBlock> DominanceFrontier;

        /// <summary>
        /// Printer friendly name of the basic block
        /// </summary>
        public string Name => $"basicblock_{BlockId}";
        
        public bool Empty => Instructions.Count == 0;
        public bool HasInstructions => Instructions.Count > 0;

        public BasicBlock(int blockId)
        {
            BlockId = blockId;
            Predecessors = new List<BasicBlock>();
            Successors = new List<BasicBlock>();
            Instructions = new List<Instruction>();
            PhiFunctions = new Dictionary<Identifier, PhiFunction>();
            Dominance = new HashSet<BasicBlock>();
            ImmediateDominator = this;
            DominanceFrontier = new HashSet<BasicBlock>();
            DominanceTreeSuccessors = new List<BasicBlock>();
            UpwardExposedIdentifiers = new HashSet<Identifier>();
            KilledIdentifiers = new HashSet<Identifier>();
            LiveOut = new HashSet<Identifier>();
        }
        
        /// <summary>
        /// Get an instruction by index with bounds checking.
        /// </summary>
        public Instruction? GetInstruction(int index)
        {
            if (index >= 0 && index < Instructions.Count)
                return Instructions[index];
            return null;
        }

        /// <summary>
        /// Once dominance information is computed, compute the immediate (closest) dominator using BFS
        /// </summary>
        public void ComputeImmediateDominator()
        {
            // Use BFS to encounter the closest dominating node guaranteed
            var queue = new Queue<BasicBlock>(Predecessors);
            while (queue.Count != 0)
            {
                var b = queue.Dequeue();
                if (Dominance.Contains(b))
                {
                    ImmediateDominator = b;
                    if (b != this)
                    {
                        ImmediateDominator.DominanceTreeSuccessors.Add(this);
                    }
                    break;
                }
                foreach (var p in b.Predecessors)
                {
                    queue.Enqueue(p);
                }
            }
        }

        /// <summary>
        /// Prerequisite information for global liveness analysis and SSA generation. Determines the variables used from
        /// predecessor blocks (upwards exposed) and variables that are redefined in this block (killed)
        /// </summary>
        public IEnumerable<Identifier> ComputeKilledAndUpwardExposed()
        {
            var globals = new HashSet<Identifier>();
            var instructions = new List<Instruction>(PhiFunctions.Values);
            instructions.AddRange(Instructions);
            foreach (var inst in instructions)
            {
                if (inst is not PhiFunction)
                {
                    foreach (var use in inst.GetUses(true))
                    {
                        if (KilledIdentifiers.Contains(use)) continue;
                        UpwardExposedIdentifiers.Add(use);
                        globals.Add(use);
                    }
                }
                foreach(var def in inst.GetDefines(true))
                {
                    KilledIdentifiers.Add(def);
                }
            }
            return globals;
        }

        public void MarkCodeGenerated(int debugFuncId)
        {
            if (_isCodeGenerated)
            {
                Console.WriteLine($"Warning: Function {debugFuncId} using already code-generated block {Name}");
            }
            _isCodeGenerated = true;
        }

        public bool IsCodeGenerated => _isCodeGenerated;

        public string ToStringWithDF()
        {
            var ret = $@"basicblock_{BlockId}: (DF = {{ ";
            for (var i = 0; i < DominanceFrontier.Count; i++)
            {
                ret += DominanceFrontier.ToArray()[i].Name;
                if (i != DominanceFrontier.Count - 1)
                {
                    ret += ", ";
                }
            }
            ret += " })";
            return ret;
        }

        public string ToStringWithUpwardExposed()
        {
            var ret = $@"basicblock_{BlockId}: (DF = {{ ";
            for (var i = 0; i < UpwardExposedIdentifiers.Count; i++)
            {
                ret += UpwardExposedIdentifiers.ToArray()[i].ToString();
                if (i != UpwardExposedIdentifiers.Count - 1)
                {
                    ret += ", ";
                }
            }
            ret += " })";
            return ret;
        }

        public string ToStringWithLiveOut()
        {
            var ret = $@"basicblock_{BlockId}: (LiveOut = {{ ";
            for (var i = 0; i < LiveOut.Count; i++)
            {
                ret += LiveOut.ToArray()[i].ToString();
                if (i != LiveOut.Count - 1)
                {
                    ret += ", ";
                }
            }
            ret += " })";
            return ret;
        }

        public string ToStringWithFollow()
        {
            var ret = $@"basicblock_{BlockId}:";
            if (Follow != null)
            {
                ret += $@" (Follow: {Follow})";
            }
            ret += $@" (Dominance tree: {{";
            for (var i = 0; i < DominanceTreeSuccessors.Count; i++)
            {
                ret += DominanceTreeSuccessors[i].ToString();
                if (i != DominanceTreeSuccessors.Count - 1)
                {
                    ret += ", ";
                }
            }
            ret += " })";
            return ret;
        }

        public string ToStringWithLoop()
        {
            var ret = $@"basicblock_{BlockId}:";
            if (IsLoopHead)
            {
                ret += " (Loop head";
                if (LoopType != LoopType.LoopNone)
                {
                    if (LoopType == LoopType.LoopPretested)
                    {
                        ret += ": pretested";
                    }
                    else if (LoopType == LoopType.LoopPosttested)
                    {
                        ret += ": posttested";
                    }
                    else if (LoopType == LoopType.LoopEndless)
                    {
                        ret += ": endless";
                    }
                }
                if (LoopLatches.Count > 0)
                {
                    ret += $@" Latch: ";
                    foreach (var latch in LoopLatches)
                    {
                        ret += $@"{latch}, ";
                    }
                }
                if (LoopFollow != null)
                {
                    ret += $@" LoopFollow: {LoopFollow}";
                }
                if (Follow != null)
                {
                    ret += $@" IfFollow: {Follow}";
                }
                ret += ")";
            }
            else if (Follow != null)
            {
                ret += $@"(IfFollow: {Follow})";
            }
            else if (LoopBreakFollow != null)
            {
                ret += $@"(BreakFollow: {LoopBreakFollow})";
            }
            return ret;
        }
        
        public override string? ToString()
        {
            return FunctionPrinter.DebugPrintBasicBlock(this);
        }
    }
}
