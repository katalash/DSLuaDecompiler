using System;
using System.Collections.Generic;
using System.Linq;
using LuaDecompilerCore.IR;

namespace LuaDecompilerCore.Passes;

/// <summary>
/// Transform from the control flow graph representation of the function to an abstract syntax tree of the
/// Lua program which can then be printed out.
/// </summary>
public class AstTransformPass : IPass
{
    public void RunOnFunction(DecompilationContext context, Function f)
    {
        // Traverse all the nodes in post-order and try to convert jumps to if statements
        var usedFollows = new HashSet<CFG.BasicBlock>();

        // Heads of for statements
        var forHeads = new HashSet<CFG.BasicBlock>();

        var relocalize = new HashSet<Identifier>();

        // Order the blocks sequentially
        for (var i = 0; i < f.BlockList.Count; i++)
        {
            f.BlockList[i].OrderNumber = i;
        }

        // Step 1: build the AST for ifs/loops based on follow information
        foreach (var node in f.PostorderTraversal(true))
        {
            // Search instructions for identifiers we need to relocalize
            foreach (var inst in node.Instructions)
            {
                if (inst is Assignment asn)
                {
                    foreach (var def in inst.GetDefines(true))
                    {
                        if (relocalize.Contains(def))
                        {
                            asn.IsLocalDeclaration = true;
                            relocalize.Remove(def);
                        }
                    }
                }
            }

            // A for loop is a pretested loop where the follow does not match the head
            if (node.LoopFollow != null && node.LoopFollow != node && 
                node.Predecessors.Count >= 2 && node.LoopType == CFG.LoopType.LoopPretested)
            {
                var loopInitializer = node.Predecessors.First(x => !node.LoopLatches.Contains(x));

                // Match a numeric for
                if (node.Instructions.Last() is 
                    Jump { Condition: BinOp { Operation: BinOp.OperationType.OpLoopCompare } loopCondition })
                {

                    var numericFor = new NumericFor
                    {
                        Limit = loopCondition.Right
                    };
                    var loopVariable = (loopCondition.Left as IdentifierReference).Identifier;
                    var incrementInstruction = node.Instructions[^2];
                    numericFor.Increment = ((incrementInstruction as Assignment).Right as BinOp).Right;

                    // Search the predecessor block for the initial assignments (i.e. the definition)
                    /*for (int i = loopInitializer.Instructions.Count() - 1; i >= 0; i--)
                    {
                        if (loopInitializer.Instructions[i] is Assignment a && a.GetDefines(true).Contains(loopvar))
                        {
                            nfor.Initial = a;
                            //if (!lua51)
                            loopInitializer.Instructions.RemoveAt(i);
                            break;
                        }
                    }*/

                    // Remove the sub instruction at the end
                    loopInitializer.Instructions.RemoveAt(loopInitializer.Instructions.Count - 2);

                    // Extract the step variable definition
                    if (loopInitializer.GetInstruction(loopInitializer.Instructions.Count - 2) is 
                        Assignment incrementAssignment)
                    {
                        numericFor.Increment = incrementAssignment.Right;
                        if (incrementAssignment.IsLocalDeclaration)
                        {
                            relocalize.Add(incrementAssignment.Left[0].Identifier);
                        }
                        loopInitializer.Instructions.RemoveAt(loopInitializer.Instructions.Count - 2);
                    }

                    // Extract the limit variable definition
                    if (loopInitializer.GetInstruction(loopInitializer.Instructions.Count - 2) is 
                        Assignment limitAssignment)
                    {
                        numericFor.Limit = limitAssignment.Right;
                        if (limitAssignment.IsLocalDeclaration)
                        {
                            relocalize.Add(limitAssignment.Left[0].Identifier);
                        }
                        loopInitializer.Instructions.RemoveAt(loopInitializer.Instructions.Count - 2);
                    }

                    // Extract the initializer variable definition
                    if (loopInitializer.GetInstruction(loopInitializer.Instructions.Count - 2) is 
                        Assignment initAssignment)
                    {
                        numericFor.Initial = initAssignment;
                        if (initAssignment.IsLocalDeclaration)
                        {
                            relocalize.Add(initAssignment.Left[0].Identifier);
                        }
                        loopInitializer.Instructions.RemoveAt(loopInitializer.Instructions.Count - 2);
                    }

                    numericFor.Body = node.Successors[1];
                    numericFor.Body.MarkCodegened(f.FunctionId);
                    if (!usedFollows.Contains(node.LoopFollow))
                    {
                        numericFor.Follow = node.LoopFollow;
                        usedFollows.Add(node.LoopFollow);
                        node.LoopFollow.MarkCodegened(f.FunctionId);
                    }
                    if (loopInitializer.GetInstruction(loopInitializer.Instructions.Count - 1) is Jump)
                    {
                        loopInitializer.Instructions[^1] = numericFor;
                    }
                    else
                    {
                        loopInitializer.Instructions.Add(numericFor);
                    }
                    node.MarkCodegened(f.FunctionId);
                    // The head might be the follow of an if statement, so do this to not codegen it
                    usedFollows.Add(node);


                    // Remove any jump instructions from the latches if they exist
                    foreach (var latch in node.LoopLatches)
                    {
                        if (latch.Instructions.Count > 0 && 
                            latch.Instructions.Last() is Jump { Conditional: false } jmp2 && 
                            jmp2.BlockDest == node)
                        {
                            latch.Instructions.RemoveAt(latch.Instructions.Count - 1);
                        }
                    }
                }

                // Match a generic for with a predecessor initializer
                else if (node.Instructions.Count > 0 && node.Instructions.Last() is Jump { Condition: BinOp loopCondition2 } &&
                         loopInitializer.Instructions.Count >= 2 && loopInitializer.Instructions[^2] is Assignment la &&
                         la.Left[0] is { } f2 && node.Instructions[0] is 
                             Assignment
                             {
                                 Right: FunctionCall { Function: IdentifierReference fci }
                             } 
                         && fci.Identifier == f2.Identifier)
                {
                    var genericFor = new GenericFor();
                    // Search the predecessor block for the initial assignment which contains the right expression
                    var right = new Expression();
                    for (var i = loopInitializer.Instructions.Count - 1; i >= 0; i--)
                    {
                        if (loopInitializer.Instructions[i] is Assignment a)
                        {
                            right = a.Right;
                            loopInitializer.Instructions.RemoveAt(i);
                            break;
                        }
                    }

                    // Loop head has the loop variables
                    if (node.Instructions.First() is Assignment a2)
                    {
                        genericFor.Iterator = new Assignment(a2.Left, right);
                        node.Instructions.RemoveAt(0);
                    }
                    else
                    {
                        throw new Exception("Unknown for pattern");
                    }

                    // Body contains more loop bytecode that can be removed
                    var body = 
                        (node.Successors[0].ReversePostorderNumber < node.Successors[1].ReversePostorderNumber) ? 
                            node.Successors[0] : node.Successors[1];
                    if (body.Instructions[0] is Assignment)
                    {
                        body.Instructions.RemoveAt(0);
                    }

                    genericFor.Body = body;
                    genericFor.Body.MarkCodegened(f.FunctionId);
                    if (!usedFollows.Contains(node.LoopFollow))
                    {
                        genericFor.Follow = node.LoopFollow;
                        usedFollows.Add(node.LoopFollow);
                        node.LoopFollow.MarkCodegened(f.FunctionId);
                    }
                    if (loopInitializer.Instructions[^1] is Jump)
                    {
                        loopInitializer.Instructions[^1] = genericFor;
                    }
                    else
                    {
                        loopInitializer.Instructions.Add(genericFor);
                    }
                    node.MarkCodegened(f.FunctionId);
                    // The head might be the follow of an if statement, so do this to not codegen it
                    usedFollows.Add(node);
                }

                // Match a while
                else if (node.Instructions.First() is Jump { Condition: { } loopCondition4 })
                {
                    var whiles = new While
                    {
                        // Loop head has condition
                        Condition = loopCondition4
                    };

                    node.Instructions.RemoveAt(node.Instructions.Count - 1);

                    //whiles.Body = (node.Successors[0].ReversePostorderNumber > node.Successors[1].ReversePostorderNumber) ? node.Successors[0] : node.Successors[1];
                    whiles.Body = node.Successors[0];
                    whiles.Body.MarkCodegened(f.FunctionId);
                    if (!usedFollows.Contains(node.LoopFollow))
                    {
                        whiles.Follow = node.LoopFollow;
                        usedFollows.Add(node.LoopFollow);
                        node.LoopFollow.MarkCodegened(f.FunctionId);
                    }
                    // If there's a goto to this loop head, replace it with the while. Otherwise replace the last instruction of this node
                    if (loopInitializer.Successors.Count == 1)
                    {
                        if (loopInitializer.Instructions.Count > 0 && loopInitializer.Instructions[^1] is Jump)
                        {
                            loopInitializer.Instructions[^1] = whiles;
                        }
                        else
                        {
                            loopInitializer.Instructions.Add(whiles);
                        }
                    }
                    else
                    {
                        node.Instructions.Add(whiles);
                    }

                    // Remove gotos in latch
                    foreach (var pred in node.Predecessors)
                    {
                        if (pred.IsLoopLatch && pred.Instructions.Last() is Jump { Conditional: false })
                        {
                            pred.Instructions.RemoveAt(pred.Instructions.Count - 1);
                        }
                    }

                    node.MarkCodegened(f.FunctionId);
                    // The head might be the follow of an if statement, so do this to not codegen it
                    usedFollows.Add(node);
                }

                // Match a repeat while (single block)
                else if (node.Instructions.Last() is Jump { Condition: { } loopCondition5 } && 
                         node.LoopLatches.Count == 1 && node.LoopLatches[0] == node)
                {
                    var whiles = new While
                    {
                        IsPostTested = true,
                        // Loop head has condition
                        Condition = loopCondition5
                    };

                    node.Instructions.RemoveAt(node.Instructions.Count - 1);

                    //whiles.Body = (node.Successors[0].ReversePostorderNumber > node.Successors[1].ReversePostorderNumber) ? node.Successors[0] : node.Successors[1];
                    whiles.Body = node.Successors[1];
                    whiles.Body.MarkCodegened(f.FunctionId);
                    if (!usedFollows.Contains(node.LoopFollow))
                    {
                        whiles.Follow = node.LoopFollow;
                        usedFollows.Add(node.LoopFollow);
                        node.LoopFollow.MarkCodegened(f.FunctionId);
                    }
                    // If there's a goto to this loop head, replace it with the while. Otherwise replace the last instruction of this node
                    if (loopInitializer.Successors.Count == 1)
                    {
                        if (loopInitializer.Instructions[^1] is Jump)
                        {
                            loopInitializer.Instructions[^1] = whiles;
                        }
                        else
                        {
                            loopInitializer.Instructions.Add(whiles);
                        }
                    }
                    else
                    {
                        node.Instructions.Add(whiles);
                    }

                    // Remove gotos in latch
                    foreach (var pred in node.Predecessors)
                    {
                        if (pred.IsLoopLatch && pred.Instructions.Last() is Jump { Conditional: false })
                        {
                            pred.Instructions.RemoveAt(pred.Instructions.Count - 1);
                        }
                    }

                    node.MarkCodegened(f.FunctionId);
                    // The head might be the follow of an if statement, so do this to not codegen it
                    usedFollows.Add(node);
                }
            }

            // repeat...until loop
            if (node.LoopType == CFG.LoopType.LoopPosttested)
            {
                var whiles = new While
                {
                    IsPostTested = true
                };

                // Loop head has condition
                if (node.LoopLatches.Count != 1 || node.LoopLatches[0].Instructions.Count == 0 ||
                    node.LoopLatches[0].Instructions.Last() is not Jump)
                {
                    throw new Exception("Unrecognized post-tested loop");
                }
                whiles.Condition = ((Jump)node.LoopLatches[0].Instructions.Last()).Condition;

                whiles.Body = node;
                if (node.LoopFollow != null && !usedFollows.Contains(node.LoopFollow))
                {
                    whiles.Follow = node.LoopFollow;
                    usedFollows.Add(node.LoopFollow);
                    node.LoopFollow.MarkCodegened(f.FunctionId);
                }

                if (node.Predecessors.Count == 2)
                {
                    var loopInitializer = node.Predecessors.First(x => x != node.LoopLatches[0]);
                    if (loopInitializer.Successors.Count == 1)
                    {
                        if (loopInitializer.Instructions.Count > 0 && loopInitializer.Instructions[^1] is Jump)
                        {
                            loopInitializer.Instructions[^1] = whiles;
                        }
                        else
                        {
                            loopInitializer.Instructions.Add(whiles);
                        }
                    }
                    else
                    {
                        whiles.IsBlockInlined = true;
                        node.IsInfiniteLoop = true;
                        node.Instructions.Insert(0, whiles);
                    }
                }
                else
                {
                    whiles.IsBlockInlined = true;
                    node.IsInfiniteLoop = true;
                    node.Instructions.Insert(0, whiles);
                }

                // Remove jumps in latch
                foreach (var pred in node.Predecessors)
                {
                    if (pred.IsLoopLatch && pred.Instructions.Last() is Jump lj)
                    {
                        pred.Instructions.RemoveAt(pred.Instructions.Count - 1);
                    }
                }

                node.MarkCodegened(f.FunctionId);
                // The head might be the follow of an if statement, so do this to not codegen it
                usedFollows.Add(node);
            }

            // Infinite while loop
            if (node.LoopType == CFG.LoopType.LoopEndless)
            {
                var whiles = new While
                {
                    // Loop head has condition
                    Condition = new Constant(true, -1),
                    Body = node
                };

                if (node.LoopFollow != null && !usedFollows.Contains(node.LoopFollow))
                {
                    whiles.Follow = node.LoopFollow;
                    usedFollows.Add(node.LoopFollow);
                    node.LoopFollow.MarkCodegened(f.FunctionId);
                }

                if (node.Predecessors.Count == 2)
                {
                    var loopInitializer = node.Predecessors.First(x => !node.LoopLatches.Contains(x));
                    if (loopInitializer.Successors.Count == 1)
                    {
                        if (loopInitializer.Instructions.Count > 0 && loopInitializer.Instructions[^1] is Jump)
                        {
                            loopInitializer.Instructions[loopInitializer.Instructions.Count - 1] = whiles;
                        }
                        else
                        {
                            loopInitializer.Instructions.Add(whiles);
                        }
                    }
                    else
                    {
                        whiles.IsBlockInlined = true;
                        node.IsInfiniteLoop = true;
                        node.Instructions.Insert(0, whiles);
                    }
                }
                else
                {
                    whiles.IsBlockInlined = true;
                    node.IsInfiniteLoop = true;
                    node.Instructions.Insert(0, whiles);
                }

                // Remove gotos in latch
                foreach (var pred in node.Predecessors)
                {
                    if (pred.IsLoopLatch && pred.Instructions.Last() is Jump { Conditional: false })
                    {
                        pred.Instructions.RemoveAt(pred.Instructions.Count - 1);
                    }
                }

                node.MarkCodegened(f.FunctionId);
                // The head might be the follow of an if statement, so do this to not codegen it
                usedFollows.Add(node);
            }

            // Pattern match for an if statement
            if (node.Follow != null && node.Instructions.Count > 0 && node.Instructions.Last() is Jump jmp)
            {
                var ifStatement = new IfStatement
                {
                    Condition = jmp.Condition
                };
                // Check for empty if block
                if (node.Successors[0] != node.Follow)
                {
                    ifStatement.True = node.Successors[0];
                    ifStatement.True.MarkCodegened(f.FunctionId);
                    if (ifStatement.True.Instructions.Last() is Jump { Conditional: false } lj)
                    {
                        if (ifStatement.True.IsBreakNode)
                        {
                            ifStatement.True.Instructions[^1] = new Break();
                        }
                        else if (ifStatement.True.IsContinueNode)
                        {
                            ifStatement.True.Instructions[^1] = new Continue();
                        }
                        else if (ifStatement.True.IsLoopLatch || !ifStatement.True.Successors[0].IsLoopHead)
                        {
                            if (!ifStatement.True.IsLoopLatch && 
                                lj.BlockDest == node.Follow && 
                                node.Successors[1] == node.Follow && 
                                ifStatement.True.OrderNumber + 1 == node.Follow.OrderNumber)
                            {
                                // Generate an empty else statement if there's a jump to the follow, the follow is the next block sequentially, and it isn't fallthrough
                                ifStatement.False = f.CreateBasicBlock();
                            }
                            ifStatement.True.Instructions.Remove(lj);
                        }
                    }
                    //if (ifStatement.True.Instructions.Last() is Jump && ifStatement.True.IsContinueNode)
                    if (node.IsContinueNode)// && node.Successors[0].IsLoopHead)
                    {
                        var bb = f.CreateBasicBlock();
                        bb.Instructions = new List<Instruction> { new Continue() };
                        ifStatement.True = bb;
                    }
                }
                if (node.Successors[1] != node.Follow)
                {
                    ifStatement.False = node.Successors[1];
                    ifStatement.False.MarkCodegened(f.FunctionId);
                    if (ifStatement.False.Instructions.Last() is Jump { Conditional: false } fj)
                    {
                        if (ifStatement.False.IsBreakNode)
                        {
                            ifStatement.False.Instructions[^1] = new Break();
                        }
                        else if (!ifStatement.False.Successors[0].IsLoopHead)
                        {
                            ifStatement.False.Instructions.Remove(fj);
                        }
                    }
                    if (node.IsContinueNode && node.Successors[1].IsLoopHead)
                    {
                        var bb = f.CreateBasicBlock();
                        bb.Instructions = new List<Instruction> { new Continue() };
                        ifStatement.False = bb;
                    }
                }
                if (!usedFollows.Contains(node.Follow))
                {
                    ifStatement.Follow = node.Follow;
                    ifStatement.Follow.MarkCodegened(f.FunctionId);
                    usedFollows.Add(node.Follow);
                }
                node.Instructions[^1] = ifStatement;
            }
        }

        // Step 2: Remove Jmp instructions from follows if they exist
        foreach (var follow in usedFollows)
        {
            if (follow.Instructions.Count > 0 && follow.Instructions.Last() is Jump jmp)
            {
                follow.Instructions.Remove(jmp);
            }
        }

        // Step 3: For debug walk the CFG and print blocks that haven't been codegened
        foreach (var b in f.PostorderTraversal(true))
        {
            if (b != f.BeginBlock && !b.Codegened())
            {
                Console.WriteLine($@"Warning: block_{b.BlockID} in function {f.FunctionId} was not used in code generation. THIS IS LIKELY A DECOMPILER BUG!");
            }
        }

        f.IsAst = true;
    }
}