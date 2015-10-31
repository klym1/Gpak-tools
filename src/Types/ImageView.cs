using System;
using System.Collections.Generic;
using System.Drawing;

namespace Types
{
    public class ImageView
    {
        private readonly int _width;
        private readonly int _height;

        public Color[] Pixels { get; set; }

        public int Width
        {
            get { return _width; }
        }

        public int Height
        {
            get { return _height; }
        }

        public ImageView(int width, int height)
        {
            _width = width;
            _height = height;

            Pixels = new Color[width * height];

            for (int i = 0; i < width*height; i++)
            {
                Pixels[i] = Color.FromArgb(0, 0, 0, 0);
            }     
        }
    }

    public static class ImageViewExtensions
    {
        public static void DrawHorizontalColorLine(this ImageView bitmap, Color[] colorArray, int offsetX, int offsetY)
        {
            Array.Copy(
                sourceArray: colorArray,
                sourceIndex: 0,
                destinationArray: bitmap.Pixels,
                destinationIndex: offsetY * bitmap.Width + offsetX,
                length: colorArray.Length);
        }

        public static void DrawColorPixel(this ImageView bitmap, Color pixel, int offsetX, int offsetY)
        {
            var width = bitmap.Width;
            bitmap.Pixels[offsetY * width + offsetX] = pixel; 
        }
    }
}