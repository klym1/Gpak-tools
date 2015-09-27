using System.Collections.ObjectModel;
using System.Linq;

namespace Types
{
    public static class MultiPictureElAbsoluteCoordinatesConverter
    {
        public static Collection<AbsoluteMultiPictureEl> ConvertToAbsoluteCoordinatesBlocks(this Collection<MultiPictureEl> elems)
        {
            return new Collection<AbsoluteMultiPictureEl>(elems.Select(ConvertToAbsoluteCoordinates).ToList());
        }

        //todo refactor
        public static AbsoluteMultiPictureEl ConvertToAbsoluteCoordinates(this MultiPictureEl elem)
        {
            var offsetX = 0;

            var absoluteBlockCollection = new Collection<AbsoluteBlock>();

            Block previousBlock = null;

            foreach (var block in elem.Collection)
            {
                if (previousBlock != null)
                {
                    offsetX += previousBlock.Length + block.Offsetx;
                }

                previousBlock = block;

                var absoluteBlock = new AbsoluteBlock(offsetX, block.Length, elem.RowIndex);

                absoluteBlockCollection.Add(absoluteBlock);
            }

            return new AbsoluteMultiPictureEl(absoluteBlockCollection);
        }
    }
}