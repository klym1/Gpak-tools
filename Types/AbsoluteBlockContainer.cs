using System.Collections.Generic;
using System.Linq;

namespace Types
{
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