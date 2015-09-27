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

        public void RenderBitmap(Bitmap bitMap, Collection<AbsoluteMultiPictureEl> piactureElements, Collection<CounterBlock> secondPartBlocks, ImageLayoutInfo layout, List<Color> imagePaletteColors)
        {
            foreach (var it in piactureElements)
            {
                foreach (var block in it.Collection)
                {
                    using (var graphics = Graphics.FromImage(bitMap))
                    {
                        graphics.FillRectangle(new SolidBrush(Color.SkyBlue),
                            new Rectangle(new Point(block.OffsetX * PixelSize + layout.offsetX, block.OffsetY * PixelSize + layout.offsetY),
                                new Size(block.Length * PixelSize, PixelSize)));

                    }
                }
            }
        }

        private CounterBlock FetchCounterBlock()
        {
            _enumerator.MoveNext();

            return _enumerator.Current;
        }

        private AbsoluteBlock FetchBlock()
        {
            _blockEnumerator.MoveNext();

            return _blockEnumerator.Current;
        }

        private List<CounterBlock>.Enumerator _enumerator;
        private List<AbsoluteBlock>.Enumerator _blockEnumerator;

        public void RenderCounterBlocksOnBitmap(Bitmap bitMap, Collection<AbsoluteMultiPictureEl> piactureElements, Collection<CounterBlock> secondPartBlocks, ImageLayoutInfo layout, List<Color> imagePaletteColors)
        {
            var blockContainerCollection = GetDistributedCounterPartBlocks(piactureElements, secondPartBlocks);

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

        private Collection<AbsoluteBlockContainer> GetDistributedCounterPartBlocks(Collection<AbsoluteMultiPictureEl> piactureElements, Collection<CounterBlock> secondPartBlocks)
        {
            var allBlocks = piactureElements.SelectMany(it => it.Collection).ToList();

            _enumerator = secondPartBlocks.ToList().GetEnumerator();
            _blockEnumerator = allBlocks.GetEnumerator();

            var blockContainerCollection = new Collection<AbsoluteBlockContainer>();

            var currentCounterBlock = FetchCounterBlock();

            var currentBlock = FetchBlock();
            var currentBlockContainer = new AbsoluteBlockContainer(currentBlock);
            blockContainerCollection.Add(currentBlockContainer);

            var blockLengthNeeded = 16;

            while (true)
            {
                int lengthAdded;

                var isSuccess = TryToAppendCounterBlock(currentBlockContainer, currentCounterBlock, blockLengthNeeded,
                    out lengthAdded);

                if (isSuccess)
                {
                    currentCounterBlock = FetchCounterBlock();
                    if (currentCounterBlock == null) break;
                    blockLengthNeeded = 16;
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

            return blockContainerCollection;
        }

        private bool TryToAppendCounterBlock(AbsoluteBlockContainer absoluteBlockContainer, CounterBlock counterBlock, int blockSizeNeeded, out int lenthAdded)
        {
            if (absoluteBlockContainer.CanAddFullBlock(blockSizeNeeded))
            {
                absoluteBlockContainer.CounterBlockContainers.Add(new CounterBlockContainer(counterBlock, blockSizeNeeded, absoluteBlockContainer.TotalSpaceOccupied, blockSizeNeeded < 16 ? (16 - blockSizeNeeded) : 0));
                lenthAdded = blockSizeNeeded;
                return true;
            }
            
            var freeSpace = absoluteBlockContainer.FreeSpaceLeft;

            absoluteBlockContainer.CounterBlockContainers.Add(new CounterBlockContainer(counterBlock, freeSpace, absoluteBlockContainer.TotalSpaceOccupied, blockSizeNeeded < 16 ? (16 - blockSizeNeeded) : 0));
            lenthAdded = freeSpace;
            return false;
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
            var initialOffsetX = offsetX;
            
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