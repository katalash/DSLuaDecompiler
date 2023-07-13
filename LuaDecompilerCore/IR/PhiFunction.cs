using System.Collections.Generic;
using System.Linq;

namespace LuaDecompilerCore.IR
{
    public class PhiFunction : Instruction
    {
        public Identifier Left;
        public List<Identifier> Right;

        public PhiFunction(Identifier left, List<Identifier> right)
        {
            Left = left;
            Right = right;
        }

        public override HashSet<Identifier> GetDefines(bool registersOnly)
        {
            return new HashSet<Identifier>(new List<Identifier> { Left });
        }

        public override HashSet<Identifier> GetUses(bool registersOnly)
        {
            var uses = new HashSet<Identifier>();
            foreach (var id in Right)
            {
                uses.UnionWith(Right);
            }
            return uses;
        }

        public override void RenameDefines(Identifier orig, Identifier newIdentifier)
        {
            if (Left == orig)
            {
                Left = newIdentifier;
            }
        }

        public override void RenameUses(Identifier orig, Identifier newIdentifier)
        {
            for (int i = 0; i < Right.Count; i++)
            {
                if (Right[i] == orig)
                {
                    if (orig != null)
                    {
                        orig.UseCount--;
                    }
                    Right[i] = newIdentifier;
                    if (newIdentifier != null)
                    {
                        newIdentifier.UseCount++;
                    }
                }
            }
        }

        public override string ToString()
        {
            string ret = $@"{Left} = phi(";
            for (int i = 0; i < Right.Count; i++)
            {
                if (Right[i] != null)
                {
                    ret += Right[i].ToString();
                }
                else
                {
                    ret += "undefined";
                }
                if (i != Right.Count - 1)
                {
                    ret += ", ";
                }
            }
            ret += ")";
            return ret;
        }
    }
}
