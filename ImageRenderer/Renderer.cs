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

        public void RenderBitmap(Bitmap bitMap, Collection<MultiPictureEl> piactureElements, Collection<CounterBlock> secondPartBlocks, ImageLayoutInfo layout, List<Color> imagePaletteColors)
        {
            foreach (var it in piactureElements)
            {
                var offsetx = 0;

                foreach (var block in it.Collection)
                {
                    offsetx += block.Offsetx;

                    using (var graphics = Graphics.FromImage(bitMap))
                    {
                        graphics.FillRectangle(new SolidBrush(Color.SkyBlue),
                            new Rectangle(new Point(offsetx * PixelSize + layout.offsetX, it.RowIndex * PixelSize + layout.offsetY),
                                new Size(block.Length * PixelSize, PixelSize)));

                    }
                    offsetx += block.Length;
                }
            }
        }

        private CounterBlock fetchCounterBlock()
        {
            enumerator.MoveNext();

            return enumerator.Current;
        }

        private Block fetchBlock()
        {
            blockEnumerator.MoveNext();

            return blockEnumerator.Current;
        }

        private List<CounterBlock>.Enumerator enumerator;
        private List<Block>.Enumerator blockEnumerator;

        public void RenderCounterBlocksOnBitmap(Bitmap bitMap, Collection<MultiPictureEl> piactureElements,
            Collection<CounterBlock> secondPartBlocks, ImageLayoutInfo layout, List<Color> imagePaletteColors)
        {
            var allOffsets2 = secondPartBlocks.ToList();
            var blockLengths = piactureElements.SelectMany(it => it.Collection).Sum(it => it.Length);

            // secondsPart blocks total length should be equal to total length of firstPartBlocks. Currently not totally true (19262 / 19264)

            var allBlocks = piactureElements.SelectMany(it => it.Collection).ToList();

            enumerator = allOffsets2.GetEnumerator();
            blockEnumerator = allBlocks.GetEnumerator();

            Block currentBlock = null;
            CounterBlock currentCounterBlock = null;
            BlockContainer currentBlockContainer = null;

            var blockContainerCollection = new Collection<BlockContainer>();

            currentCounterBlock = fetchCounterBlock();

            currentBlock = fetchBlock();
            currentBlockContainer = new BlockContainer(currentBlock);
            blockContainerCollection.Add(currentBlockContainer);

            var blockLengthNeeded = 16;

            while(true)
            {
                int lengthAdded;

                var isSuccess = TryToAppendCounterBlock(currentBlockContainer, currentCounterBlock, blockLengthNeeded, out lengthAdded);

                if (isSuccess)
                {
                    currentCounterBlock = fetchCounterBlock();
                    if(currentCounterBlock == null) break;
                    blockLengthNeeded = 16;
                }
                else
                {
                    currentBlock = fetchBlock();
                    if(currentBlock == null) break;
                    currentBlockContainer = new BlockContainer(currentBlock);
                    blockContainerCollection.Add(currentBlockContainer);

                    blockLengthNeeded -= lengthAdded;
                }
            }

            var g = blockContainerCollection;
        }
        
        private bool TryToAppendCounterBlock(BlockContainer blockContainer, CounterBlock counterBlock, int blockSizeNeeded, out int lenthAdded)
        {
            if (blockContainer.CanAddFullBlock(blockSizeNeeded))
            {
                blockContainer.CounterBlockContainers.Add(new CounterBlockContainer(counterBlock, blockSizeNeeded));
                lenthAdded = blockSizeNeeded;
                return true;
            }
            
            var freeSpace = blockContainer.FreeSpaceLeft;

            blockContainer.CounterBlockContainers.Add(new CounterBlockContainer(counterBlock, freeSpace));
            lenthAdded = freeSpace;
            return false;
        }

        public void RenderCounterBlocksOnBitmap2(Bitmap bitMap, Collection<MultiPictureEl> piactureElements, Collection<CounterBlock> secondPartBlocks, ImageLayoutInfo layout, List<Color> imagePaletteColors)
        {
            var allOffsets2 = secondPartBlocks.Select(it => it).ToList();
            var blockLengths = piactureElements.SelectMany(it => it.Collection).Sum(it => it.Length);

            // secondsPart blocks total length should be equal to total length of firstPartBlocks. Currently not totally true (19262 / 19264)

            enumerator = allOffsets2.GetEnumerator();
            
            var blockLengthUsed = 0;
            var currentRow = 0;
            var previousBlockUsed = 0;
            CounterBlock previousBlock = null;

            var borrow = false;

            foreach (var it in piactureElements)
            {
                var offsetx = 0;

                foreach (var block in it.Collection)
                {
                    offsetx += block.Offsetx;

                    //draw Remaining
                    if (borrow)
                    {
                        var remaining = 16 - previousBlockUsed;
                        var activeBlock = previousBlock;

                        var slice =
                              imagePaletteColors.Skip(activeBlock.Offset + previousBlockUsed)
                                  .Take(32)
                                  .ToList();

                        //var slice = Enumerable.Range(1, 20).Select(i => Color.Green).ToArray();

                        DrawHorizontalColorLine(bitMap, slice,
                            offsetx + layout.offsetX + blockLengthUsed,
                            currentRow + layout.offsetY);

                        blockLengthUsed += remaining;
                        borrow = false;

                    }

                        while (block.Length - blockLengthUsed >= 16)
                        {
                            var currentCounterBlock = fetchCounterBlock();

                            if(currentCounterBlock == null) return;
                            
                            var slice =
                                imagePaletteColors.Skip(currentCounterBlock.Offset)
                                    .Take(block.Length - blockLengthUsed)
                                    .ToList();

                            DrawHorizontalColorLine(bitMap, slice,
                                offsetx + layout.offsetX + blockLengthUsed, 
                                currentRow + layout.offsetY);

                            blockLengthUsed += 16;
                        }

                        var diff = block.Length - blockLengthUsed;

                    if (diff > 0)
                    {
                        var currentCounterBlock = fetchCounterBlock();

                        if(currentCounterBlock == null) return;
                        
                        var usedLength = block.Length - blockLengthUsed;

                        previousBlockUsed = usedLength; ;

                        previousBlock = currentCounterBlock;

                        var slice =
                            imagePaletteColors.Skip(currentCounterBlock.Offset)
                                .Take(usedLength)
                                .ToList();

                        DrawHorizontalColorLine(bitMap, slice,
                            offsetx + layout.offsetX + blockLengthUsed,
                            currentRow + layout.offsetY);

                        borrow = true;
                    }

                    blockLengthUsed = 0;
                   offsetx += block.Length;

                }
                
                currentRow++;

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