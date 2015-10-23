using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms.VisualStyles;
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