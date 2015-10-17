using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using Types;

namespace GPExtractor
{
    public class ImageGenerator
    {
        public void RenderCounterBlocksOnBitmap(ImageView imageView, List<AbsoluteBlock> piactureElements, Collection<RawColorBlock> secondPartBlocks, ImageLayoutInfo layout, List<Color> imagePaletteColors)
        {
            var blocksDistributor = new BlocksDistributor();

            var blockContainerCollection = blocksDistributor.GetDistributedCounterPartBlocks(piactureElements, secondPartBlocks);
            var i = 0;

            foreach (var blockContainer in blockContainerCollection)
            {
                foreach (var counterBlockContainer in blockContainer.CounterBlockContainers)
                {
                    var slice = imagePaletteColors.Skip(counterBlockContainer.RawColorBlock.Offset + counterBlockContainer.StripePadding)
                                 .Take(counterBlockContainer.Width)
                                 .ToList();
                    
                    var redColorSlice =
                        Enumerable.Range(1, counterBlockContainer.Width).Select(it => i % 2 == 0 ? Color.Red : Color.Green).ToList();

                    i++;

                    

                    imageView.DrawHorizontalColorLine(slice,
                        layout.offsetX + blockContainer.Block.OffsetX + counterBlockContainer.Offset,
                        layout.offsetY + blockContainer.Block.OffsetY);
                }
            }
        }

        public static List<Color> OffsetsToColors(byte[] imagePaletteOffsets, Collection<Color> colorCollection)
        {
            return imagePaletteOffsets.Select(offset => colorCollection[offset]).ToList();
        }
    }
}
