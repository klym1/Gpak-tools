using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms.VisualStyles;
using Types;

namespace ImageRenderer
{
    public class BitmapRenderer : IRenderer
    {
        public void RenderPalette(Bitmap bitmap, ICollection<Color> colorCollection, int width, int pixelSize)
        {
            var z = 0;
            var y = 0;

            foreach (var color in colorCollection)
            {
                var brush = new SolidBrush(color);

                using (var graphics = Graphics.FromImage(bitmap))
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

        public void SetupCanvas(Bitmap bitmap)
        {
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.FillRectangle(new SolidBrush(Color.White),
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            }
        }

        public void RenderImage(Bitmap bitmap, ImageView imageView)
        {
            var lockedBitmap = new LockBitmap(bitmap);
            lockedBitmap.LockBits();

            for (int i = 0; i < imageView.Height; i++)
                for (int j = 0; j < imageView.Width; j++)
                {
                    if (imageView.Pixels[j, i] != Color.FromArgb(0, 0, 0, 0))
                    {
                        lockedBitmap.SetPixel(j, i, imageView.Pixels[j, i]);
                    }
                }

            lockedBitmap.UnlockBits();
        }
    }
}