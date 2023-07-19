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

        public override HashSet<Identifier> GetDefines(bool registersOnly)
        {
            return new HashSet<Identifier>(new List<Identifier> { Left });
        }

        public override HashSet<Identifier> GetUses(bool registersOnly)
        {
            var uses = new HashSet<Identifier>();
            foreach (var id in Right)
            {
                if (id != null)
                    uses.Add(id);
            }
            return uses;
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
