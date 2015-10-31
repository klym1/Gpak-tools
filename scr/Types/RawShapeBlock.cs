using System.Diagnostics;

namespace Types
{
    [DebuggerDisplay("{offsetx} {length}")]
    public class RawShapeBlock
    {
        public int Offsetx { get; set; }
        public int Length { get; set; }

        public RawShapeBlock(int offsetx, int length)
        {
            Offsetx = offsetx;
            Length = length;
        }
    }
}