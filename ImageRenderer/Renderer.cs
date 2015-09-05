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
                        graphics.FillRectangle(new SolidBrush(Color.Green),
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
            var blockNumber = 0;

            var grps = piactureElements.SplitByGroups();

            foreach (var grp in grps)
            {
                foreach (var block in grp.FirstColumnBlocks)
                {
                    var stripe = colorCollection
                        .Skip(currentStripeOffset)
                        .Take(block.length)
                        .ToList();

                    DrawHorizontalColorLine(bitMap, stripe,
                        offsetX: block.offsetx + layout.offsetX,
                        offsetY: rowIndex + layout.offsetY);
                    
                    rowIndex++;

                }

                currentStripeOffset += grp.Length;
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