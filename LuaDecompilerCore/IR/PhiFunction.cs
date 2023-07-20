using System.Collections.Generic;
using System.Linq;

namespace LuaDecompilerCore.IR
{
    public sealed class PhiFunction : Instruction
    {
        public Identifier Left;
        public readonly List<Identifier?> Right;

        public PhiFunction(Identifier left, List<Identifier?> right)
        {
            Left = left;
            Right = right;
        }

        public override void GetDefines(HashSet<Identifier> defines, bool registersOnly)
        {
            defines.Add(Left);
        }

        public override void GetUses(HashSet<Identifier> uses, bool registersOnly)
        {
            foreach (var id in Right)
            {
                if (id != null)
                    uses.Add(id);
            }
        }

        public override void RenameDefines(Identifier original, Identifier newIdentifier)
        {
            if (Left == original)
            {
                Left = newIdentifier;
            }
        }

        public override void RenameUses(Identifier original, Identifier? newIdentifier)
        {
            for (var i = 0; i < Right.Count; i++)
            {
                if (Right[i] != original) continue;
                original.UseCount--;
                Right[i] = newIdentifier;
                if (newIdentifier != null)
                    newIdentifier.UseCount++;
            }
        }
    }
}
