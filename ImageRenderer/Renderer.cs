using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using Types;

namespace ImageRenderer
{
    public class Renderer : IRenderer
    {
        const int PixelSize = 1;

        public void RenderBitmap(Bitmap bitMap, Collection<MultiPictureEl> piactureElements, Collection<CounterSection> secondPartBlocks, ImageLayoutInfo layout, List<Color> imagePaletteColors)
        {
            foreach (var it in piactureElements)
            {
                var offsetx = 0;

                foreach (var block in it.Collection)
                {
                    offsetx += block.offsetx;

                    using (var graphics = Graphics.FromImage(bitMap))
                    {
                        graphics.FillRectangle(new SolidBrush(Color.SkyBlue),
                            new Rectangle(new Point(offsetx * PixelSize + layout.offsetX, it.RowIndex * PixelSize + layout.offsetY),
                                new Size(block.length * PixelSize, PixelSize)));

                    }
                    offsetx += block.length;
                }
            }
        }

        private CounterBlock fetchBlockAndMove()
        {
            enumerator.MoveNext();

            return enumerator.Current;
        }

        private List<CounterBlock>.Enumerator enumerator;

        public void RenderCounterBlocksOnBitmap(Bitmap bitMap, Collection<MultiPictureEl> piactureElements, Collection<CounterSection> secondPartBlocks, ImageLayoutInfo layout, List<Color> imagePaletteColors)
        {
            var allOffsets2 = secondPartBlocks.SelectMany(it => it.SecondPartBlocks).ToList();
            var blockLengths = piactureElements.SelectMany(it => it.Collection).Sum(it => it.length);

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
                    offsetx += block.offsetx;

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

                        while (block.length - blockLengthUsed >= 16)
                        {
                            var currentCounterBlock = fetchBlockAndMove();

                            if(currentCounterBlock == null) return;
                            
                            var slice =
                                imagePaletteColors.Skip(currentCounterBlock.Offset)
                                    .Take(block.length - blockLengthUsed)
                                    .ToList();

                            DrawHorizontalColorLine(bitMap, slice,
                                offsetx + layout.offsetX + blockLengthUsed, 
                                currentRow + layout.offsetY);

                            blockLengthUsed += 16;
                        }

                        var diff = block.length - blockLengthUsed;

                    if (diff > 0)
                    {
                        var currentCounterBlock = fetchBlockAndMove();

                        if(currentCounterBlock == null) return;
                        
                        var usedLength = block.length - blockLengthUsed;

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
                   offsetx += block.length;

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