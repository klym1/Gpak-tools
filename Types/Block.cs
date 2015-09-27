using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Types
{
    [DebuggerDisplay("{offsetx} {length}")]
    public class Block
    {
        public int Offsetx { get; set; }
        public int Length { get; set; }

        public Block(int offsetx, int length)
        {
            Offsetx = offsetx;
            Length = length;
        }

        public bool Equals(Block other)
        {
            return Offsetx == other.Offsetx && Length == other.Length;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Block && Equals((Block) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Offsetx*397) ^ Length;
            }
        }
    }

    [DebuggerDisplay("{OffsetX} {OffsetY} {Length}")]
    public class AbsoluteBlock
    {
        public int OffsetX { get; set; }
        public int Length { get; set; }
        public int OffsetY { get; set; }

        public AbsoluteBlock(int offsetX, int length, int offsetY)
        {
            OffsetX = offsetX;
            Length = length;
            OffsetY = offsetY;
        }

        public bool Equals(AbsoluteBlock other)
        {
            return OffsetX == other.OffsetX && Length == other.Length;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is AbsoluteBlock && Equals((AbsoluteBlock)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (OffsetX * 397) ^ Length;
            }
        }
    }

    public class AbsoluteBlockContainer
    {
        public AbsoluteBlock Block;
        public List<CounterBlockContainer> CounterBlockContainers;

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
            CounterBlockContainers = new List<CounterBlockContainer>();
        }
    }
}