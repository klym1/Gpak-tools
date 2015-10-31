using System.Diagnostics;

namespace Types
{
    [DebuggerDisplay("{OffsetX} {OffsetY} {Length}")]
    public class AbsoluteBlock
    {
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public int Length { get; set; }

        public AbsoluteBlock(int offsetX, int length, int offsetY)
        {
            OffsetX = offsetX;
            Length = length;
            OffsetY = offsetY;
        }
    }
}