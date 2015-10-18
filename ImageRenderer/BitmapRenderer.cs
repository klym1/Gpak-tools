using System.Collections.Generic;
using System.Drawing;
using Types;

namespace ImageRenderer
{
    public class BitmapRenderer : IRenderer
    {
        private readonly Bitmap _bitmap;

        public BitmapRenderer(Bitmap bitmap)
        {
            this._bitmap = bitmap;
        }

        public void RenderPalette(ICollection<Color> colorCollection, int width, int pixelSize)
        {
            var z = 0;
            var y = 0;

            foreach (var color in colorCollection)
            {
                var brush = new SolidBrush(color);

                using (var graphics = Graphics.FromImage(_bitmap))
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
        }

        public void SetupCanvas()
        {
            using (var graphics = Graphics.FromImage(_bitmap))
            {
                graphics.FillRectangle(new SolidBrush(Color.White),
                    new Rectangle(0, 0, _bitmap.Width, _bitmap.Height));
            }
        }

        public void RenderImage(ImageView imageView)
        {
            var lockedBitmap = new LockBitmap(_bitmap);

            lockedBitmap.LockBits();

            for (int i = 0; i < imageView.Height; i++)
                for (int j = 0; j < imageView.Width; j++)
                {
                    lockedBitmap.SetPixel(i, j, imageView.Pixels[i, j]);
                }

            lockedBitmap.UnlockBits();
        }
    }
}