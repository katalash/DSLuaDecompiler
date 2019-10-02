using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace luadec.IR
{
    /// <summary>
    /// A label that represents a jump target
    /// </summary>
    public class Label : IInstruction
    {
        /// <summary>
        /// Used to generate unique label names
        /// </summary>
        public static int LabelCount = 0;

        /// <summary>
        /// How many instructions use this label. Used to delete labels in some optimizations
        /// </summary>
        public int UsageCount = 0;

        public string LabelName;

        public Label()
        {
            LabelName = $@"Label_{LabelCount}";
            LabelCount++;
        }

        public override string ToString()
        {
            return $@"{LabelName}:";
        }
    }
}
