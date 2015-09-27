using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Types;

namespace ImageRenderer
{
    public class Renderer : IRenderer
    {
        const int PixelSize = 1;

        public void RenderBitmap(Bitmap bitMap, List<AbsoluteBlock> piactureElements, Collection<CounterBlock> secondPartBlocks, ImageLayoutInfo layout, List<Color> imagePaletteColors)
        {
            foreach (var block in piactureElements)
            {
                //foreach (var block in it.Collection)
                {
                    using (var graphics = Graphics.FromImage(bitMap))
                    {
                        graphics.FillRectangle(new SolidBrush(Color.Gray),
                            new Rectangle(new Point(block.OffsetX * PixelSize + layout.offsetX, block.OffsetY * PixelSize + layout.offsetY),
                                new Size(block.Length * PixelSize, PixelSize)));

                    }
                }
            }
        }

        public void RenderCounterBlocksOnBitmap(Bitmap bitMap, List<AbsoluteBlock> piactureElements, Collection<CounterBlock> secondPartBlocks, ImageLayoutInfo layout, List<Color> imagePaletteColors)
        {
            var blocksDistributor = new BlocksDistributor();

            var blockContainerCollection = blocksDistributor.GetDistributedCounterPartBlocks(piactureElements, secondPartBlocks);

            foreach (var blockContainer in blockContainerCollection)
            {
                foreach (var counterBlockContainer in blockContainer.CounterBlockContainers)
                {
                    var slice =
                             imagePaletteColors.Skip(counterBlockContainer.CounterBlock.Offset + counterBlockContainer.StripePadding)
                                 .Take(counterBlockContainer.Width)
                                 .ToList();
                    
                    DrawHorizontalColorLine(bitMap, slice,
                        layout.offsetX + blockContainer.Block.OffsetX + counterBlockContainer.Offset,
                        layout.offsetY + blockContainer.Block.OffsetY );
                }
            }
        }

        public Bitmap RenderPalette(ICollection<Color> colorCollection, int width, int pixelSize)
        {
            var newBitmap = new Bitmap(500, 500);

            var z = 0;
            var y = 0;
            
            foreach (var color in colorCollection)
            {
                var brush = new SolidBrush(color);

                using (var graphics = Graphics.FromImage(newBitmap))
                {
                    graphics.FillRectangle(brush, new Rectangle(new Point(z, y), new Size(pixelSize, pixelSize)));
                }

                z += pixelSize;

                if (z >= width * pixelSize)
                {
                    z = 0;
                    y += pixelSize;
                }

            }

            return newBitmap;
        }

        public void DrawHorizontalColorLine(Bitmap bitmap, ICollection<Color> colorCollection, int offsetX, int offsetY, int height = 1)
        {
            var initialOffsetX = (int)offsetX;
            
            foreach (var color in colorCollection)
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.FillRectangle(new SolidBrush(color),
                        new Rectangle(new Point(initialOffsetX++, offsetY),
                            new Size(PixelSize, height)));
                }
            }
        }
    }
}