using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

    public class AbsoluteBlockContainer
    {
        public AbsoluteBlock Block;
        public List<RawColorBlockContainer> CounterBlockContainers;

        public int TotalSpaceOccupied
        {
            get { return CounterBlockContainers.Sum(it => it.Width); }
        }

        public int FreeSpaceLeft
        {
            get { return Block.Length - TotalSpaceOccupied; }
        }
        
        public bool CanAddFullBlock(int lengthNeeded)
        {
            return Block.Length >= CounterBlockContainers.Sum(it => it.Width) + lengthNeeded; 
        }

        public AbsoluteBlockContainer(AbsoluteBlock block)
        {
            Block = block;
            CounterBlockContainers = new List<RawColorBlockContainer>();
        }
    }
}