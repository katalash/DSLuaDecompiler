using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace luadec.IR
{
    public class WhileStatement : IInstruction
    {
        public Expression Condition;
        public CFG.BasicBlock Body;
        public CFG.BasicBlock Follow;
    }
}
