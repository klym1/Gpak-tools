using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Types;

namespace GPExtractor
{
    public static class MultiPictureElAbsoluteCoordinatesConverter
    {
        public static List<AbsoluteBlock> ConvertToAbsoluteCoordinatesBlocks(this IEnumerable<RawShapeBlocksGroup> elems)
        {
            return new List<AbsoluteBlock>(elems.Select(ConvertToAbsoluteCoordinates).SelectMany(it => it).ToList());
        }
        
        private static List<AbsoluteBlock> ConvertToAbsoluteCoordinates(this RawShapeBlocksGroup elem)
        {
            var offsetX = 0;

            var absoluteBlockCollection = new Collection<AbsoluteBlock>();

            RawShapeBlock previousRawShapeBlock = null;

            foreach (var block in elem.Collection)
            {
                if (previousRawShapeBlock != null)
                {
                    offsetX += previousRawShapeBlock.Length;
                }

                offsetX += block.Offsetx;

                previousRawShapeBlock = block;

                var absoluteBlock = new AbsoluteBlock(offsetX, block.Length, elem.RowIndex);

                absoluteBlockCollection.Add(absoluteBlock);
            }

            return absoluteBlockCollection.ToList();
        }
    }
}