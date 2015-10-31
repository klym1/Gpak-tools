using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Types
{
    //possibly Merging is not necessary
    public static class MultiPictureElMerger
    {
        public static Collection<RawShapeBlocksGroup> MergeBlocks(this Collection<RawShapeBlocksGroup> elems)
        {
            return new Collection<RawShapeBlocksGroup>(elems.Select(MergeBlocks).ToList());
        }

        public static RawShapeBlocksGroup MergeBlocks(this RawShapeBlocksGroup elem)
        {
            var blocks = elem.Collection;

            if (blocks.Count < 2) return elem;

            var newElem = new RawShapeBlocksGroup(new List<RawShapeBlock>(), elem.RowIndex);
            
            var currentBlock = blocks[0];

            for (int i = 1; i < blocks.Count; i++)
            {
                var nextBlock = blocks[i];

                if (nextBlock.Offsetx == 0)
                {
                    currentBlock.Length += nextBlock.Length;
                }
                else
                {
                    newElem.Collection.Add(new RawShapeBlock(currentBlock.Offsetx, currentBlock.Length));

                    currentBlock = nextBlock;
                }
            }

            newElem.Collection.Add(new RawShapeBlock(currentBlock.Offsetx, currentBlock.Length));

            return newElem;
        }
    }
}