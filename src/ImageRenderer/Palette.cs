using System;
using System.Drawing;
using System.IO;

namespace ImageRenderer
{
    /// <summary>A 256-color palette, read from a <c>.pal</c> file (256 RGB triplets).</summary>
    public sealed class Palette
    {
        public const int ColorCount = 256;

        private readonly Color[] _colors;

        private Palette(Color[] colors)
        {
            _colors = colors;
        }

        public Color this[int index] => _colors[index];

        public static Palette Load(string path)
        {
            return Parse(File.ReadAllBytes(path));
        }

        /// <summary>Reads the first 768 bytes; some game palettes carry extra bytes after them.</summary>
        public static Palette Parse(byte[] bytes)
        {
            if (bytes.Length < ColorCount * 3)
            {
                throw new InvalidDataException("A palette needs " + ColorCount * 3 + " bytes, got " + bytes.Length + ".");
            }

            var colors = new Color[ColorCount];
            for (var i = 0; i < ColorCount; i++)
            {
                colors[i] = Color.FromArgb(255, bytes[3 * i], bytes[3 * i + 1], bytes[3 * i + 2]);
            }

            return new Palette(colors);
        }
    }
}
