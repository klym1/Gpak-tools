using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using Types;

namespace ImageRenderer
{
    public class Renderer : IRenderer
    {
        const int PixelSize = 1;

        public void RenderBitmap(Bitmap bitMap, Collection<MultiPictureEl> piactureElements, ImageLayoutInfo layout)
        {
            var random = new Random();

            foreach (var it in piactureElements)
            {
                var offsetx = 0;

                foreach (var block in it.Collection)
                {
                    offsetx += block.offsetx;

                    var randomColor = Color.FromArgb(255, block.length, 0, 0);

                    using (var graphics = Graphics.FromImage(bitMap))
                    {
                        graphics.FillRectangle(new SolidBrush(randomColor),
                            new Rectangle(new Point(offsetx * PixelSize + layout.offsetX, it.RowIndex * PixelSize + layout.offsetY),
                                new Size(block.length * PixelSize, PixelSize)));
                    }
                    offsetx += block.length;
                }
            }
        }
    }
}