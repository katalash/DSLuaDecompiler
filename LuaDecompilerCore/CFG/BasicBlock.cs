using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace luadec.CFG
{
    /// <summary>
    /// A block of instructions in which control flow only enters at the beginning and leaves at the end
    /// </summary>
    public class BasicBlock
    {
        private static int BlockIDCounter = 0;

        public int BlockID;
        public List<BasicBlock> Predecessors;
        public List<BasicBlock> Successors;
        public List<IR.IInstruction> Instructions;
        public Dictionary<IR.Identifier, IR.PhiFunction> PhiFunctions;

        public HashSet<IR.Identifier> PhiMerged = new HashSet<IR.Identifier>();

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
        public HashSet<IR.Identifier> UpwardExposedIdentifiers;
        public HashSet<IR.Identifier> KilledIdentifiers;
        public HashSet<IR.Identifier> LiveOut;

        // Control flow analysis
        public int ReversePostorderNumber = 0;
        public bool IsLoopHead = false;
        public bool IsLoopLatch = false;
        public BasicBlock LoopFollow = null;
        public BasicBlock LoopLatch = null;
        public CFG.LoopType LoopType = LoopType.LoopNone;
        public BasicBlock Follow = null;
        public BasicBlock LoopBreakFollow = null;
        public BasicBlock LoopContinueFollow = null;
        public bool IsBreakNode = false;
        public bool IsContinueNode = false;
        // Set to true if both the true and false branch lead to a return
        public bool IfOrphaned = false;

        // Code gen
        public bool IsInfiniteLoop = false;
        private bool IsCodegened = false;

        /// <summary>
        /// Used for SSA construction
        /// </summary>
        public HashSet<BasicBlock> DominanceFrontier;

        public BasicBlock()
        {
            BlockID = BlockIDCounter;
            BlockIDCounter++;
            Predecessors = new List<BasicBlock>();
            Successors = new List<BasicBlock>();
            Instructions = new List<IR.IInstruction>();
            PhiFunctions = new Dictionary<IR.Identifier, IR.PhiFunction>();
            Dominance = new HashSet<BasicBlock>();
            DominanceFrontier = new HashSet<BasicBlock>();
            DominanceTreeSuccessors = new List<BasicBlock>();
            UpwardExposedIdentifiers = new HashSet<IR.Identifier>();
            KilledIdentifiers = new HashSet<IR.Identifier>();
            LiveOut = new HashSet<IR.Identifier>();
        }

        /// <summary>
        /// Once dominance information is computed, compute the immediate (closest) dominator using BFS
        /// </summary>
        public void ComputeImmediateDominator()
        {
            // Use BFS to encounter the closest dominating node guaranteed
            Queue<BasicBlock> queue = new Queue<BasicBlock>(Predecessors);
            while (queue.Count() != 0)
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
        public HashSet<IR.Identifier> ComputeKilledAndUpwardExposed()
        {
            var globals = new HashSet<IR.Identifier>();
            var instructions = new List<IR.IInstruction>(PhiFunctions.Values);
            instructions.AddRange(Instructions);
            foreach (var inst in instructions)
            {
                if (!(inst is IR.PhiFunction))
                {
                    foreach (var use in inst.GetUses(true))
                    {
                        if (!KilledIdentifiers.Contains(use))
                        {
                            UpwardExposedIdentifiers.Add(use);
                            globals.Add(use);
                        }
                    }
                }
                foreach(var def in inst.GetDefines(true))
                {
                    KilledIdentifiers.Add(def);
                }
            }
            return globals;
        }

        public void MarkCodegened(int debugFuncID)
        {
            if (IsCodegened)
            {
                Console.WriteLine("Warning: Function " + debugFuncID + " using already codegened block " + ToString());
            }
            IsCodegened = true;
        }

        public bool Codegened()
        {
            return IsCodegened;
        }

        public static void ResetCounter()
        {
            BlockIDCounter = 0;
        }

        public string GetName()
        {
            return $@"basicblock_{BlockID}";
        }

        public string PrintBlock(int indentLevel, bool infloopprint=false)
        {
            string ret = "";
            //ret += $@"basicblock_{BlockID}:";
            //ret += "\n";
            int count = (IsInfiniteLoop && !infloopprint) ? 1 : Instructions.Count();
            int begin = (IsInfiniteLoop && infloopprint) ? 1 : 0;
            for (int j = begin; j < count; j++)
            {
                var inst = Instructions[j];
                for (int i = 0; i < indentLevel; i++)
                {
                    ret += "    ";
                }
                ret += inst.WriteLua(indentLevel);
                if (!(inst is IR.IfStatement) && j != Instructions.Count() - 1)
                {
                    ret += "\n";
                }
            }
            return ret;
        }

        public string ToStringWithDF()
        {
            var ret = $@"basicblock_{BlockID}: (DF = {{ ";
            for (int i = 0; i < DominanceFrontier.Count(); i++)
            {
                ret += DominanceFrontier.ToArray()[i].GetName();
                if (i != DominanceFrontier.Count() - 1)
                {
                    ret += ", ";
                }
            }
            ret += " })";
            return ret;
        }

        public string ToStringWithUpwardExposed()
        {
            var ret = $@"basicblock_{BlockID}: (DF = {{ ";
            for (int i = 0; i < UpwardExposedIdentifiers.Count(); i++)
            {
                ret += UpwardExposedIdentifiers.ToArray()[i].ToString();
                if (i != UpwardExposedIdentifiers.Count() - 1)
                {
                    ret += ", ";
                }
            }
            ret += " })";
            return ret;
        }

        public string ToStringWithLiveOut()
        {
            var ret = $@"basicblock_{BlockID}: (LiveOut = {{ ";
            for (int i = 0; i < LiveOut.Count(); i++)
            {
                ret += LiveOut.ToArray()[i].ToString();
                if (i != LiveOut.Count() - 1)
                {
                    ret += ", ";
                }
            }
            ret += " })";
            return ret;
        }

        public string ToStringWithFollow()
        {
            var ret = $@"basicblock_{BlockID}:";
            if (Follow != null)
            {
                ret += $@" (Follow: {Follow})";
            }
            ret += $@" (Dominance tree: {{";
            for (int i = 0; i < DominanceTreeSuccessors.Count(); i++)
            {
                ret += DominanceTreeSuccessors[i].ToString();
                if (i != DominanceTreeSuccessors.Count() - 1)
                {
                    ret += ", ";
                }
            }
            ret += " })";
            return ret;
        }

        public string ToStringWithLoop()
        {
            var ret = $@"basicblock_{BlockID}:";
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
                if (LoopLatch != null)
                {
                    ret += $@" Latch: {LoopLatch}";
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

        public override string ToString()
        {
            return $@"basicblock_{BlockID}:";
        }
    }
}
