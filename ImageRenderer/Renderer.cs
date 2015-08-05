using System.Collections.ObjectModel;
using System.Drawing;
using Types;

namespace ImageRenderer
{
    public class Renderer : IRenderer
    {
        public  Bitmap RenderBitmap(Collection<MultiPictureEl> piactureElements)
        {
            var newBitmap2 = new Bitmap(700, 700);
            var pixelSize = 2;

            using (var graphics = Graphics.FromImage(newBitmap2))
            {
                graphics.FillRectangle(new SolidBrush(Color.SpringGreen),
                    new Rectangle(0, 0, newBitmap2.Width, newBitmap2.Height));
            }

            foreach (var it in piactureElements)
            {
                var color = Color.Black;

                var brush = new SolidBrush(color);

                var offsetx = 0;

                foreach (var block in it.Collection)
                {
                    offsetx += block.offsetx;

                    using (var graphics = Graphics.FromImage(newBitmap2))
                    {
                        graphics.FillRectangle(brush,
                            new Rectangle(new Point(offsetx * pixelSize, it.RowIndex * pixelSize),
                                new Size(block.length * pixelSize, pixelSize)));
                    }
                    offsetx += block.length;
                }
            }

            return newBitmap2;
        }
    }
}