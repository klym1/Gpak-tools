using System;
using System.IO;

namespace GPExtractor
{
    /// <summary>
    /// A color table (<c>*.grd</c>): rows of 256 palette indices with no header. Each row maps every palette
    /// color to another one (shades, fog, blends of two colors).
    /// </summary>
    public sealed class GrdTable
    {
        public const int RowLength = 256;

        private readonly byte[] _bytes;

        private GrdTable(byte[] bytes)
        {
            _bytes = bytes;
            Rows = bytes.Length / RowLength;
        }

        public int Rows { get; }

        /// <summary>The palette index a row maps a color to.</summary>
        public byte this[int row, int color]
        {
            get
            {
                if (row < 0 || row >= Rows)
                {
                    throw new ArgumentOutOfRangeException(nameof(row));
                }

                if (color < 0 || color >= RowLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(color));
                }

                return _bytes[row * RowLength + color];
            }
        }

        public ReadOnlySpan<byte> Row(int row)
        {
            if (row < 0 || row >= Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            return _bytes.AsSpan(row * RowLength, RowLength);
        }

        public static GrdTable Load(string path)
        {
            return Parse(File.ReadAllBytes(path));
        }

        public static GrdTable Parse(byte[] bytes)
        {
            if (bytes.Length == 0 || bytes.Length % RowLength != 0)
            {
                throw new InvalidDataException("Not a GRD table: its size is not a multiple of 256 bytes.");
            }

            return new GrdTable((byte[]) bytes.Clone());
        }
    }
}
