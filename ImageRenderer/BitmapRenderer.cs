using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using Types;

namespace ImageRenderer
{
    public class BitmapRenderer : IRenderer
    {
        public void RenderBitmap(Bitmap bitMap, List<AbsoluteBlock> piactureElements, ImageLayoutInfo layout)
        {
            using (var graphics = Graphics.FromImage(bitMap))
            {
                foreach (var block in piactureElements)
                {
                    graphics.FillRectangle(new SolidBrush(Color.Gray),
                        new Rectangle(
                            new Point(block.OffsetX + layout.offsetX, block.OffsetY + layout.offsetY),
                            new Size(block.Length, 1)));
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

        public void DrawHorizontalColorLine(Bitmap bitmap, ICollection<Color> colorCollection, int offsetX, int offsetY)
        {
            var initialOffsetX = offsetX;

            foreach (var color in colorCollection)
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.FillRectangle(new SolidBrush(color),
                        new Rectangle(new Point(initialOffsetX++, offsetY),
                            new Size(1, 1)));
                }
            }
        }

        public void SetupCanvas(Bitmap bitMap)
        {
            using (var graphics = Graphics.FromImage(bitMap))
            {
                graphics.FillRectangle(new SolidBrush(Color.White),
                    new Rectangle(0, 0, bitMap.Width, bitMap.Height));
            }
        }
    }
}