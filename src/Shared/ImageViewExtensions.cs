using System.Drawing;
using System.Drawing.Imaging;
using Types;

namespace Shared
{
    public static class ImageViewBitmapExtensions
    {
        /// <summary>
        /// Converts a rendered <see cref="ImageView"/> into a GDI+ bitmap (Windows only).
        /// </summary>
        public static Bitmap ToBitmap(this ImageView imageView)
        {
            var bitmap = new Bitmap(imageView.Width, imageView.Height, PixelFormat.Format32bppArgb);

            var lockedBitmap = new LockBitmap(bitmap);
            lockedBitmap.LockBits();

            for (int y = 0; y < imageView.Height; y++)
                for (int x = 0; x < imageView.Width; x++)
                {
                    lockedBitmap.SetPixel(x, y, imageView.Pixels[y * imageView.Width + x]);
                }

            lockedBitmap.UnlockBits();

            return bitmap;
        }
    }
}
