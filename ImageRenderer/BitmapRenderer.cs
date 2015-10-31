using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms.VisualStyles;
using Shared;
using Types;

namespace ImageRenderer
{
    public class BitmapRenderer : IRenderer
    {
        public void SetupCanvas(Bitmap bitmap)
        {
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.FillRectangle(new SolidBrush(Color.FromArgb(255, 0x80, 0x80, 0x80)),
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            }
        }

        public void RenderImage(Bitmap bitmap, ImageView imageView)
        {
            var lockedBitmap = new LockBitmap(bitmap);
            lockedBitmap.LockBits();

            var width = imageView.Width;

            for (int i = 0; i < imageView.Height; i++)
                for (int j = 0; j < imageView.Width; j++)
                {
                    if (imageView.Pixels[i * width + j] != Color.FromArgb(0, 0, 0, 0))
                    {
                        lockedBitmap.SetPixel(j, i, imageView.Pixels[i * width + j]);
                    }
                }

            lockedBitmap.UnlockBits();
        }
    }
}