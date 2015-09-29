using System.Collections.Generic;
using System.Drawing;

namespace Types
{
    public class ImageView
    {
        public Color[,] Pixels { get; set; }

        public int Width
        {
            get { return Pixels.GetLength(1); }
        }

        public int Height
        {
            get { return Pixels.GetLength(0); }
        }

        public ImageView(int width, int height)
        {
            Pixels = new Color[width,height];
        }
    }

    public static class ImageViewExtensions
    {
        public static void DrawHorizontalColorLine(this ImageView bitmap, ICollection<Color> colorCollection, int offsetX, int offsetY)
        {
            var initialOffsetX = offsetX;

            foreach (var color in colorCollection)
            {
                bitmap.Pixels[offsetY, initialOffsetX++] = color; 
            }
        }
    }
}