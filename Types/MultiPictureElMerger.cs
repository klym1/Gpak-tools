using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Types
{
    public static class MultiPictureElMerger
    {
        public static Collection<MultiPictureEl> MergeBlocks(this Collection<MultiPictureEl> elems)
        {
            return new Collection<MultiPictureEl>(elems.Select(MergeBlocks).ToList());
        }

        public static MultiPictureEl MergeBlocks(this MultiPictureEl elem)
        {
            var blocks = elem.Collection;

            if (blocks.Count < 2) return elem;

            var newElem = new MultiPictureEl(new Collection<Block>(), elem.RowIndex);
            
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
                    newElem.Collection.Add(new Block
                    {
                        Length = currentBlock.Length,
                        Offsetx = currentBlock.Offsetx,
                        OffsetY = elem.RowIndex
                    });

                    currentBlock = nextBlock;
                }
            }

            newElem.Collection.Add(new Block
            {
                Length = currentBlock.Length,
                Offsetx = currentBlock.Offsetx,
                OffsetY = elem.RowIndex
            });

            return newElem;
        }
    }
}