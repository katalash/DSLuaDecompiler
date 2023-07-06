using System.Linq;

namespace LuaDecompilerCore.IR
{
    public class While : IInstruction
    {
        public Expression Condition;

        public CFG.BasicBlock Body;
        public CFG.BasicBlock Follow;

        public bool IsPostTested = false;
        public bool IsBlockInlined = false;

        public override string WriteLua(int indentLevel)
        {
            string ret = "";
            if (IsPostTested)
            {
                ret = $@"repeat" + "\n";
            }
            else
            {
                ret = $@"while {Condition} do" + "\n";
            }

            ret += Body.PrintBlock(indentLevel + 1, IsBlockInlined);
            ret += "\n";
            for (int i = 0; i < indentLevel; i++)
            {
                ret += "    ";
            }
            if (IsPostTested)
            {
                ret += $@"until {Condition}";
            }
            else
            {
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
