using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace luadec.IR
{
    public class PhiFunction : IInstruction
    {
        public Identifier Left;
        public List<Identifier> Right;

        public PhiFunction(Identifier left, List<Identifier> right)
        {
            Left = left;
            Right = right;
        }

        public override HashSet<Identifier> GetDefines(bool regonly)
        {
            return new HashSet<Identifier>(new List<Identifier>() { Left });
        }

        public override HashSet<Identifier> GetUses(bool regonly)
        {
            var uses = new HashSet<Identifier>();
            foreach (var id in Right)
            {
                uses.UnionWith(Right);
            }
            return uses;
        }

        public override void RenameDefines(Identifier orig, Identifier newi)
        {
            if (Left == orig)
            {
                Left = newi;
            }
        }

        public override void RenameUses(Identifier orig, Identifier newi)
        {
            for (int i = 0; i < Right.Count(); i++)
            {
                if (Right[i] == orig)
                {
                    if (orig != null)
                    {
                        orig.UseCount--;
                    }
                    Right[i] = newi;
                    if (newi != null)
                    {
                        newi.UseCount++;
                    }
                }
            }
        }

        public override string ToString()
        {
            string ret = $@"{Left} = phi(";
            for (int i = 0; i < Right.Count(); i++)
            {
                if (Right[i] != null)
                {
                    ret += Right[i].ToString();
                }
                else
                {
                    ret += "undefined";
                }
                if (i != Right.Count() - 1)
                {
                    ret += ", ";
                }
            }
            ret += ")";
            return ret;
        }
    }
}
