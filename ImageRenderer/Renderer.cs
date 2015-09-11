using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms.VisualStyles;
using GPExtractor;
using Types;

namespace ImageRenderer
{
    public class Renderer : IRenderer
    {
        const int PixelSize = 1;

        public void RenderBitmap(Bitmap bitMap, Collection<MultiPictureEl> piactureElements, ImageLayoutInfo layout)
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

        public void RenderColorStripesOnBitmap(Bitmap bitMap, Collection<MultiPictureEl> piactureElements, ImageLayoutInfo layout,
            ICollection<Color> colorCollection)
        {
            var currentStripeOffset = 0;
            var rowIndex = 0;
            var initStripeOffset = 0;

            MultiPictureEl previousRow = null;

            foreach (var row in piactureElements)
            {
                var lineoffset = (int)layout.offsetX;

                if (rowIndex > 3 || RowsDiffer(previousRow, row))
                {
                    rowIndex = 0;
                    initStripeOffset = currentStripeOffset;
                }
                
                currentStripeOffset = initStripeOffset; 
                
                foreach (var block in row.Collection)
                {
                    var stripe = colorCollection
                        .Skip(currentStripeOffset)
                        .Take(block.length)
                        .ToList();

                    lineoffset += block.offsetx ;

                    DrawHorizontalColorLine(bitMap, stripe,
                        offsetX: lineoffset,
                        offsetY: row.RowIndex + layout.offsetY);

                    lineoffset += block.length;
                    currentStripeOffset += block.length;
                }

                rowIndex++;
                previousRow = row;
            }
        }

        private static bool RowsDiffer(MultiPictureEl previousRow, MultiPictureEl row)
        {
            var firstBlockDiffers = previousRow != null &&
                                    (!MultiPictureEl.Equals(previousRow, row));
            return firstBlockDiffers;
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

        public void DrawHorizontalColorLine(Bitmap bitmap, ICollection<Color> colorCollection, int offsetX, int offsetY)
        {
            var initialOffsetX = offsetX;
            
            foreach (var color in colorCollection)
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.FillRectangle(new SolidBrush(color),
                        new Rectangle(new Point(initialOffsetX++, offsetY),
                            new Size(PixelSize, 1)));
                }
            }
        }
    }
}