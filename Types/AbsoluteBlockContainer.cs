using System.Collections.Generic;

namespace Types
{
    public class AbsoluteBlockContainer
    {
        public AbsoluteBlock Block;
        private readonly List<RawColorBlockContainer> _counterBlockContainers;

        public List<RawColorBlockContainer> CounterBlockContainers
        {
            get { return _counterBlockContainers; }
        }

        public void Add(RawColorBlockContainer blockContainer)
        {
            _counterBlockContainers.Add(blockContainer);

            TotalSpaceOccupied += blockContainer.Width;
            FreeSpaceLeft -= blockContainer.Width;
        }

        public int TotalSpaceOccupied { get; private set; }
        public int FreeSpaceLeft { get; private set; }
        
        public bool CanAddFullBlock(int lengthNeeded)
        {
            return Block.Length >= TotalSpaceOccupied + lengthNeeded; 
        }

        public AbsoluteBlockContainer(AbsoluteBlock block)
        {
            Block = block;
            _counterBlockContainers = new List<RawColorBlockContainer>();
            FreeSpaceLeft = Block.Length;
        }
    }
}