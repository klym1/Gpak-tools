using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Types
{
    public class BlocksDistributor
    {
        public Collection<AbsoluteBlockContainer> GetDistributedCounterPartBlocks(List<AbsoluteBlock> piactureElements, Collection<RawColorBlock> secondPartBlocks)
        {
            var blockContainerCollection = GetDistributedCounterPartBlocksInternal(piactureElements, secondPartBlocks);

            VerifyBlockContainerCollection(blockContainerCollection);

            return blockContainerCollection;
        }

        public Collection<AbsoluteBlockContainer> GetDistributedCounterPartBlocksInternal(List<AbsoluteBlock> piactureElements, Collection<RawColorBlock> secondPartBlocks)
        {
            _enumerator = secondPartBlocks.ToList().GetEnumerator();
            _blockEnumerator = piactureElements.ToList().GetEnumerator();

            var blockContainerCollection = new Collection<AbsoluteBlockContainer>();

            var currentCounterBlock = FetchCounterBlock();

            var currentBlock = FetchBlock();
            var currentBlockContainer = new AbsoluteBlockContainer(currentBlock);
            blockContainerCollection.Add(currentBlockContainer);

            var blockLengthNeeded = GetBlockLengthNeeded(currentCounterBlock);

            while (true)
            {
                int lengthAdded;

                if (TryToAppendCounterBlock(currentBlockContainer, currentCounterBlock, blockLengthNeeded, out lengthAdded))
                {
                    currentCounterBlock = FetchCounterBlock();
                    if (currentCounterBlock == null) break;
                    blockLengthNeeded = GetBlockLengthNeeded(currentCounterBlock);
                }
                else
                {
                    currentBlock = FetchBlock();
                    if (currentBlock == null) break;
                    currentBlockContainer = new AbsoluteBlockContainer(currentBlock);
                    blockContainerCollection.Add(currentBlockContainer);

                    blockLengthNeeded -= lengthAdded;
                }
            }

            _enumerator.Dispose();
            _blockEnumerator.Dispose();

            return blockContainerCollection;
        }

        private bool TryToAppendCounterBlock(AbsoluteBlockContainer absoluteBlockContainer, RawColorBlock rawColorBlock, int blockSizeNeeded, out int lenthAdded)
        {
            var stripePadding = blockSizeNeeded < GetBlockLengthNeeded(rawColorBlock) ? (GetBlockLengthNeeded(rawColorBlock) - blockSizeNeeded) : 0; ;

            if (absoluteBlockContainer.CanAddFullBlock(blockSizeNeeded))
            {
                absoluteBlockContainer.CounterBlockContainers.Add(new RawColorBlockContainer(rawColorBlock, blockSizeNeeded, absoluteBlockContainer.TotalSpaceOccupied, stripePadding));
                lenthAdded = blockSizeNeeded;
                return true;
            }

            var freeSpace = absoluteBlockContainer.FreeSpaceLeft;

            absoluteBlockContainer.CounterBlockContainers.Add(new RawColorBlockContainer(rawColorBlock, freeSpace, absoluteBlockContainer.TotalSpaceOccupied, stripePadding));
            lenthAdded = freeSpace;
            return false;
        }

        private RawColorBlock FetchCounterBlock()
        {
            _enumerator.MoveNext();
            return _enumerator.Current;
        }

        private AbsoluteBlock FetchBlock()
        {
            _blockEnumerator.MoveNext();
            return _blockEnumerator.Current;
        }

        private List<RawColorBlock>.Enumerator _enumerator;
        private List<AbsoluteBlock>.Enumerator _blockEnumerator;

        private int GetBlockLengthNeeded(RawColorBlock rawColorBlock)
        {
            var type = rawColorBlock.ThirdOctet;
            return type + 3;
        }

        private void VerifyBlockContainerCollection(Collection<AbsoluteBlockContainer> blockContainerCollection)
        {
            var all = blockContainerCollection.All(it => it.CounterBlockContainers.Sum(o => o.Width) == it.Block.Length);
            if (!all)
            {
                throw new Exception("23423");
            }
        }

    }
}