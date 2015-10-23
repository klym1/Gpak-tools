using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using Types;

namespace GPExtractor
{
    public class ImageGenerator
    {
        public void RenderCounterBlocksOnBitmap(ImageView imageView, List<AbsoluteBlock> piactureElements, Collection<RawColorBlock> secondPartBlocks, ImageLayoutInfo layout, List<Color> imagePaletteColors, List<Color> generalPaletteColors)
        {
            var blocksDistributor = new BlocksDistributor();

            var blockContainerCollection = blocksDistributor.GetDistributedCounterPartBlocks(piactureElements, secondPartBlocks);

            foreach (var blockContainer in blockContainerCollection)
            {
                foreach (var counterBlockContainer in blockContainer.CounterBlockContainers)
                {
                    if (counterBlockContainer.RawColorBlock.type == RawColorBlockType.MultiPiexl)
                    {
                        var slice = imagePaletteColors.Skip(counterBlockContainer.RawColorBlock.Offset + counterBlockContainer.StripePadding)
                                .Take(counterBlockContainer.Width)
                                .ToList();

                        imageView.DrawHorizontalColorLine(slice,
                            layout.offsetX + blockContainer.Block.OffsetX + counterBlockContainer.Offset,
                            layout.offsetY + blockContainer.Block.OffsetY);
                    }
                    else
                    {
                        var colorIndex = counterBlockContainer.RawColorBlock.One;

                        var color = generalPaletteColors[colorIndex];

                        imageView.DrawColorPixel(color, 
                           layout.offsetX + blockContainer.Block.OffsetX + counterBlockContainer.Offset,
                           layout.offsetY + blockContainer.Block.OffsetY);
                    }
                }
            }
        }

        public void RenderShapeBlocks(ImageView imageView, List<AbsoluteBlock> piactureElements,
            ImageLayoutInfo layout)
        {
            foreach (var block in piactureElements)
            {
                var slice = Enumerable.Range(1, block.Length).Select(it => Color.FromArgb(0x99,0,0,0)).ToList();

                imageView.DrawHorizontalColorLine(slice,
                          layout.offsetX + block.OffsetX,
                          layout.offsetY + block.OffsetY);
            }
        }

        public static List<Color> OffsetsToColors(byte[] imagePaletteOffsets, Collection<Color> colorCollection)
        {
            return imagePaletteOffsets.Select(offset => colorCollection[offset]).ToList();
        }
    }
}
