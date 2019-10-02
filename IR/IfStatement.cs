using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace luadec.IR
{
    /// <summary>
    /// Higher level AST node for encoding if statements
    /// </summary>
    class IfStatement : IInstruction
    {
        public Expression Condition;
        public CFG.BasicBlock True = null;
        public CFG.BasicBlock False = null;
        public CFG.BasicBlock Follow = null;
        public bool IsElseIf = false;

        public override string WriteLua(int indentLevel)
        {
            string ret = "";
            if (IsElseIf)
            {
                ret = $@"elseif {Condition} then" + "\n";
            }
            else
            {
                ret = $@"if {Condition} then" + "\n";
            }
            ret += True.PrintBlock(indentLevel + 1);
            if (False != null)
            {
                ret += "\n";
                // Check for elseif
                if (False.Instructions.Count() == 1 && False.Instructions.First() is IfStatement s && s.Follow == null)
                {
                    s.IsElseIf = true;
                    ret += False.PrintBlock(indentLevel);
                }
                else
                {
                    for (int i = 0; i < indentLevel; i++)
                    {
                        ret += "    ";
                    }
                    ret += "else\n";
                    ret += False.PrintBlock(indentLevel + 1);
                }
            }
            ret += "\n";
            if (!IsElseIf)
            {
                for (int i = 0; i < indentLevel; i++)
                {
                    ret += "    ";
                }
                ret += "end";
            }
            if (Follow != null && Follow.Instructions.Count() > 0)
            {
                ret += "\n";
                ret += Follow.PrintBlock(indentLevel);
            }
            return ret;
        }
    }
}
