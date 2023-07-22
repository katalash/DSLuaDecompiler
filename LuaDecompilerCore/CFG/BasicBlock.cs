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
        public int BlockIndex;
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

        /// <summary>
        /// First instruction in instruction list
        /// </summary>
        public Instruction First => Instructions.First();

        /// <summary>
        /// Last instruction in instruction list
        /// </summary>
        public Instruction Last
        {
            get => Instructions[^1];
            set => Instructions[^1] = value;
        }

        /// <summary>
        /// If block is a condition, the successor block for when the condition is "true"
        /// </summary>
        public BasicBlock EdgeTrue
        {
            get => Successors[0];
            set => Successors[0] = value;
        }
        
        /// <summary>
        /// If block is a condition, the successor block for when the condition is "false"
        /// </summary>
        public BasicBlock EdgeFalse
        {
            get => Successors[1];
            set => Successors[1] = value;
        }

        /// <summary>
        /// Block is a conditional block where a successor is chosen based on a conditional jump
        /// </summary>
        public bool IsConditionalJump => Successors.Count == 2 && Last is ConditionalJump;

        /// <summary>
        /// Block ends with an unconditional jump with a single successor
        /// </summary>
        public bool IsUnconditionalJump => Successors.Count == 1 && Last is Jump;
        
        /// <summary>
        /// Block ends with an explicit conditional or unconditional jump to another block
        /// </summary>
        public bool IsJump => Last is Jump or ConditionalJump;
        
        /// <summary>
        /// Block is a return block that ends the function call when the block is done executing
        /// </summary>
        public bool IsReturn => Successors.Count == 1 && Last is Return;

        /// <summary>
        /// Block falls through to the next logical block in the program order without an explicit jump or return
        /// </summary>
        public bool IsFallthrough => Successors.Count == 1 && !IsJump && !IsReturn;
        
        public bool Empty => Instructions.Count == 0;
        public bool HasInstructions => Instructions.Count > 0;

        /// <summary>
        /// Debug validation of block to ensure it is well formed and no unexpected/illegal states have formed. Validation
        /// is local to the block and does not validate the entire CFG.
        /// </summary>
        public void Validate(BasicBlock beginBlock, BasicBlock endBlock)
        {
            // End block should have >1 predecessor, no successors, and no instructions
            if (this == endBlock)
            {
                if (endBlock.Predecessors.Count == 0)
                    throw new Exception($@"End block {Name} has no predecessors");
                if (endBlock.Successors.Count > 0)
                    throw new Exception($@"End block {Name} has successors");
                if (endBlock.HasInstructions)
                    throw new Exception($@"End block has instructions");
                return;
            }
            
            // Begin block should have no predecessors
            if (this == beginBlock && Predecessors.Count > 0)
                throw new Exception($@"Begin block {Name} has predecessors");
            
            // Ensure all successors have us as a predecessor
            foreach (var successor in Successors)
            {
                if (!successor.Predecessors.Contains(this))
                    throw new Exception($@"{Name} successor {successor} does not contain {Name} as predecessor");
            }
            
            // Ensure all predecessors have us as a successors
            foreach (var predecessor in Predecessors)
            {
                if (!predecessor.Successors.Contains(this))
                    throw new Exception($@"{Name} predecessor {predecessor} does not contain {Name} as successor");
            }
            
            // All blocks except the end block should have 1 or 2 successors in current implementation
            if (Successors.Count is < 1 or > 2)
                throw new Exception($@"Block {Name} has {Successors.Count} successors");
            
            // Blocks with a single successor should not have a conditional jump
            if (Successors.Count == 1 && HasInstructions && Last is ConditionalJump)
                throw new Exception($@"Block {Name} has 1 successor but ends with conditional jump");
            
            // Blocks with a two successors must have a conditional jump
            if (Successors.Count == 2 && (Empty || Last is not ConditionalJump))
                throw new Exception($@"Block {Name} has 2 successor but does not end with conditional jump");
            
            // Blocks that end with return should have a single end block as successor
            if (HasInstructions && Last is Return && (Successors.Count != 1 || Successors[0] == endBlock))
                throw new Exception($@"Block {Name} has return but does not have end block as single successor");
        }
        
        public BasicBlock(int blockId)
        {
            BlockId = blockId;
            Predecessors = new List<BasicBlock>();
            Successors = new List<BasicBlock>();
            Instructions = new List<Instruction>(10);
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
        /// Adds a new instruction to the block
        /// </summary>
        /// <param name="instruction">The instruction to add</param>
        public void AddInstruction(Instruction instruction)
        {
            instruction.Block = this;
            Instructions.Add(instruction);
        }

        /// <summary>
        /// Inserts an instruction at the specified index
        /// </summary>
        /// <param name="index">The index to insert the instruction at</param>
        /// <param name="instruction">The instruction to insert</param>
        public void InsertInstruction(int index, Instruction instruction)
        {
            instruction.Block = this;
            Instructions.Insert(0, instruction);
        }

        /// <summary>
        /// Adds a new successor to the block
        /// </summary>
        /// <param name="successor">The block to add as a successor</param>
        public void AddSuccessor(BasicBlock successor)
        {
            Successors.Add(successor);
            successor.Predecessors.Add(this);
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
            var definesSet = new HashSet<Identifier>(2);
            var usesSet = new HashSet<Identifier>(10);
            var globals = new HashSet<Identifier>(Instructions.Count / 4);
            var instructions = new List<Instruction>(PhiFunctions.Values);
            UpwardExposedIdentifiers.EnsureCapacity(Instructions.Count / 4);
            KilledIdentifiers.EnsureCapacity(Instructions.Count / 4);
            instructions.AddRange(Instructions);
            foreach (var inst in instructions)
            {
                if (inst is not PhiFunction)
                {
                    usesSet.Clear();
                    inst.GetUses(usesSet, true);
                    foreach (var use in usesSet)
                    {
                        if (KilledIdentifiers.Contains(use)) continue;
                        UpwardExposedIdentifiers.Add(use);
                        globals.Add(use);
                    }
                }
                definesSet.Clear();
                inst.GetDefines(definesSet, true);
                foreach(var def in definesSet)
                {
                    KilledIdentifiers.Add(def);
                }
            }
            return globals;
        }

        public void MarkCodeGenerated(int debugFuncId, List<string> warnings)
        {
            if (_isCodeGenerated)
            {
                warnings.Add($"-- Warning: Function {debugFuncId} using already code-generated block {Name}");
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
