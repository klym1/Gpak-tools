using System.Drawing;

namespace Types
{
    /// <summary>An RGBA image in memory, stored row by row.</summary>
    public class ImageView
    {
        public Color[] Pixels { get; }

        public int Width { get; }

        public int Height { get; }

        /// <summary>Creates a fully transparent image.</summary>
        public ImageView(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color[width * height];
        }
    }
}
