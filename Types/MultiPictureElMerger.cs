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

        public static ICollection<MultiPictureElGroup> SplitByGroups(this Collection<MultiPictureEl> elems)
        {
            var singleColumnCollection =
                elems.Where(it => it.Collection.Count >= 1).Select(it => it.Collection[0]).ToList();
            

            int i = 0;

            var tempBlockCollection = new Collection<Block>();
            var grpCollection = new Collection<MultiPictureElGroup>();
            var grpIndex = 0;
            var rowsUsed = 0;

            while(i < singleColumnCollection.Count - 1)
            {
                var current = singleColumnCollection[i].length;
                var next = singleColumnCollection[i+1].length;

                tempBlockCollection.Add(new Block
                {
                    length = singleColumnCollection[i].length,
                    offsetx = singleColumnCollection[i].offsetx,
                });

                rowsUsed++;
                  
                if (current != next || rowsUsed == 4)
                {

                    grpCollection.Add(new MultiPictureElGroup
                    {
                        GroupIndex = grpIndex++,
                        FirstColumnBlocks = tempBlockCollection

                    });

                    rowsUsed = 0;
                    tempBlockCollection = new Collection<Block>();
                }

                i++;
                
            }

            return grpCollection;
        }

        public static MultiPictureEl MergeBlocks(this MultiPictureEl elem)
        {
            var blocks = elem.Collection;

            if (blocks.Count < 2) return elem;

            var newElem = new MultiPictureEl
            {
                RowIndex = elem.RowIndex
            };

            var currentBlock = blocks[0];

            for (int i = 1; i < blocks.Count; i++)
            {
                var nextBlock = blocks[i];

                if (nextBlock.offsetx == 0)
                {
                    currentBlock.length += nextBlock.length;
                }
                else
                {
                    newElem.Collection.Add(new Block
                    {
                        length = currentBlock.length,
                        offsetx = currentBlock.offsetx
                    });

                    currentBlock = nextBlock;
                }
            }

            newElem.Collection.Add(new Block
            {
                length = currentBlock.length,
                offsetx = currentBlock.offsetx
            });

            return newElem;
        }
    }
}