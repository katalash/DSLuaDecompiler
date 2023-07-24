using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// If conditional structuring won't detect if statements that lead to a break or continue.
/// This pass aims to identify and structure those.
/// </summary>
public class DetectLoopBreakContinuePass : IPass
{
    public void RunOnFunction(DecompilationContext decompilationContext, FunctionContext functionContext, Function f)
    {
        var visited = new HashSet<CFG.BasicBlock>();
        void Visit(CFG.BasicBlock b, CFG.BasicBlock? loopHead)
        {
            visited.Add(b);
            var nextHead = b.IsLoopHead ? b : loopHead;
            foreach (var successor in b.Successors.Where(successor => !visited.Contains(successor)))
            {
                Visit(successor, nextHead);
            }
        
            // Detect unstructured if statements
            if (nextHead != null && b.IsConditionalJump && 
                b is not { IsLoopHead: true, LoopType: CFG.LoopType.LoopPretested })
            {
                Debug.Assert(nextHead.LoopFollow != null);
                
                // An if statement is unstructured but recoverable if it has a forward edge to the loop follow (break)
                // or head (continue) on the left or right
                foreach (var successor in b.DominanceTreeSuccessors)
                {
                    if (successor.IsLoopLatch)
                    {
                        continue;
                    }
        
                    // Mark breaks
                    if (successor.Successors.Contains(nextHead.LoopFollow))
                    {
                        successor.IsBreakNode = true;
                        b.LoopBreakFollow = nextHead.LoopFollow;
                    }
                    // Mark continues
                    if (successor.Successors.Contains(nextHead))
                    {
                        successor.IsContinueNode = true;
                        b.LoopContinueFollow = nextHead.LoopContinueFollow;
                    }
                }
            }
        }
        Visit(f.BeginBlock, null);
    }
}