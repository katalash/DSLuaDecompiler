using System.Linq;
using LuaDecompilerCore.CFG;

namespace LuaDecompilerCore.IR
{
    public class While : Instruction
    {
        public required Expression Condition;

        public required BasicBlock Body;
        public BasicBlock? Follow = null;

        public bool IsPostTested = false;
        public bool IsBlockInlined = false;
    }
}
