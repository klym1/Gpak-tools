using System;
using System.Drawing;
using Types;

namespace ImageRenderer
{
    /// <summary>
    /// Composes image layers onto an in-memory ARGB canvas. Platform independent:
    /// uses only System.Drawing.Color (System.Drawing.Primitives), not GDI+.
    /// </summary>
    public class BitmapRenderer : IRenderer
    {
        private static readonly Color BackgroundColor = Color.FromArgb(255, 0x80, 0x80, 0x80);
        private static readonly Color TransparentColor = Color.FromArgb(0, 0, 0, 0);

        public void SetupCanvas(ImageView canvas)
        {
            Array.Fill(canvas.Pixels, BackgroundColor);
        }

        // Non-transparent pixels replace canvas pixels as-is (no alpha blending),
        // matching the original GDI+ SetPixel behaviour.
        public void RenderImage(ImageView canvas, ImageView imageView)
        {
            for (int y = 0; y < imageView.Height; y++)
                for (int x = 0; x < imageView.Width; x++)
                {
                    var pixel = imageView.Pixels[y * imageView.Width + x];

                    if (pixel != TransparentColor)
                    {
                        canvas.Pixels[y * canvas.Width + x] = pixel;
                    }
                }
        }
    }
}
