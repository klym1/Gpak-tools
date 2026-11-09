using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Types;

namespace Viewer
{
    public static class ImageViewExtensions
    {
        /// <summary>
        /// Copies a rendered <see cref="ImageView"/> into an Avalonia bitmap.
        /// Pixels are copied as-is (straight, not premultiplied alpha), like the
        /// GDI+ Format32bppArgb bitmaps the WinForms client used.
        /// </summary>
        public static WriteableBitmap ToWriteableBitmap(this ImageView imageView)
        {
            var bitmap = new WriteableBitmap(
                new PixelSize(imageView.Width, imageView.Height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);

            // Color.ToArgb() is 0xAARRGGBB, i.e. B, G, R, A bytes in little-endian
            // memory: exactly Bgra8888.
            var row = new int[imageView.Width];

            using (var buffer = bitmap.Lock())
            {
                for (int y = 0; y < imageView.Height; y++)
                {
                    for (int x = 0; x < imageView.Width; x++)
                    {
                        row[x] = imageView.Pixels[y * imageView.Width + x].ToArgb();
                    }

                    Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, row.Length);
                }
            }

            return bitmap;
        }
    }
}
