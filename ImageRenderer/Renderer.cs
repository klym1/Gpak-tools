using System.Collections.ObjectModel;
using System.Drawing;
using Types;

namespace ImageRenderer
{
    public class Renderer : IRenderer
    {
        const int PixelSize = 1;

        public  Bitmap RenderBitmap(Collection<MultiPictureEl> piactureElements, ImageLayoutInfo layout)
        {
            var bitMap = new Bitmap(layout.Width + layout.offsetX, layout.Height + layout.offsetY);
            
            using (var graphics = Graphics.FromImage(bitMap))
            {
                graphics.FillRectangle(new SolidBrush(Color.SpringGreen),
                    new Rectangle(0, 0, bitMap.Width, bitMap.Height));
            }

            foreach (var it in piactureElements)
            {
                var color = Color.Black;
                var brush = new SolidBrush(color);

                var offsetx = 0;

                foreach (var block in it.Collection)
                {
                    offsetx += block.offsetx;

                    using (var graphics = Graphics.FromImage(bitMap))
                    {
                        graphics.FillRectangle(brush,
                            new Rectangle(new Point(offsetx * PixelSize + layout.offsetX, it.RowIndex * PixelSize + layout.offsetY),
                                new Size(block.length * PixelSize, PixelSize)));
                    }
                    offsetx += block.length;
                }
            }

            return bitMap;
        }
    }
}