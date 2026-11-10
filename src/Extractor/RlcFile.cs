using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace GPExtractor
{
    /// <summary>
    /// An RLC sprite file (<c>*.rlc</c>): fonts, cursors and some older sprites of Cossacks.
    /// </summary>
    /// <remarks>
    /// File layout (little-endian):
    /// <code>
    /// 0x00  "RLC" 0x1B
    /// 0x04  uint32  number of frames N
    /// 0x08  uint32 * N  frame offsets
    /// </code>
    /// A frame is uint16 width, uint16 height, then one entry per row: a byte with the number of runs, and
    /// per run a byte of transparent pixels to skip, a byte with the length and that many palette indices.
    /// </remarks>
    public sealed class RlcFile
    {
        private const int HeaderSize = 8;

        private RlcFile(IReadOnlyList<RlcFrame> frames)
        {
            Frames = frames;
        }

        public IReadOnlyList<RlcFrame> Frames { get; }

        public static RlcFile Load(string path)
        {
            return Parse(File.ReadAllBytes(path));
        }

        public static RlcFile Parse(byte[] bytes)
        {
            if (bytes.Length < HeaderSize || bytes[0] != 'R' || bytes[1] != 'L' || bytes[2] != 'C' || bytes[3] != 0x1B)
            {
                throw new InvalidDataException("Not an RLC file: missing RLC signature.");
            }

            var frameCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
            if (HeaderSize + 4L * frameCount > bytes.Length)
            {
                throw new InvalidDataException("Corrupt RLC file: frame table does not fit in the file.");
            }

            var frames = new List<RlcFrame>((int) frameCount);
            for (var i = 0; i < frameCount; i++)
            {
                var offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(HeaderSize + 4 * i));
                frames.Add(RlcFrame.Parse(bytes, offset, i));
            }

            return new RlcFile(frames);
        }
    }

    /// <summary>A run of pixels in a row: palette indices starting at column <see cref="X"/>.</summary>
    public sealed record RlcRun(int X, byte[] Pixels);

    public sealed class RlcFrame
    {
        private RlcFrame(int index, int width, int height, IReadOnlyList<IReadOnlyList<RlcRun>> rows)
        {
            Index = index;
            Width = width;
            Height = height;
            Rows = rows;
        }

        public int Index { get; }

        public int Width { get; }

        public int Height { get; }

        public IReadOnlyList<IReadOnlyList<RlcRun>> Rows { get; }

        /// <summary>Palette indices row by row, with -1 for transparent pixels.</summary>
        public short[] DecodePixels()
        {
            var pixels = new short[Width * Height];
            Array.Fill(pixels, (short) -1);

            for (var y = 0; y < Height; y++)
            {
                foreach (var run in Rows[y])
                {
                    for (var i = 0; i < run.Pixels.Length; i++)
                    {
                        pixels[y * Width + run.X + i] = run.Pixels[i];
                    }
                }
            }

            return pixels;
        }

        internal static RlcFrame Parse(byte[] bytes, long offset, int index)
        {
            if (offset + 4 > bytes.Length)
            {
                throw Corrupt(index, "starts outside the file");
            }

            var position = (int) offset;
            var width = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(position));
            var height = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(position + 2));
            position += 4;

            var rows = new List<IReadOnlyList<RlcRun>>(height);
            for (var y = 0; y < height; y++)
            {
                if (position >= bytes.Length)
                {
                    throw Corrupt(index, "ends before its last row");
                }

                var runCount = bytes[position++];
                var runs = new List<RlcRun>(runCount);
                var x = 0;
                for (var i = 0; i < runCount; i++)
                {
                    if (position + 2 > bytes.Length)
                    {
                        throw Corrupt(index, "ends inside a row");
                    }

                    x += bytes[position];
                    var length = bytes[position + 1];
                    position += 2;

                    if (position + length > bytes.Length)
                    {
                        throw Corrupt(index, "ends inside a run");
                    }

                    if (x + length > width)
                    {
                        throw Corrupt(index, "has a run wider than the frame");
                    }

                    runs.Add(new RlcRun(x, bytes.AsSpan(position, length).ToArray()));
                    position += length;
                    x += length;
                }

                rows.Add(runs);
            }

            return new RlcFrame(index, width, height, rows);
        }

        private static InvalidDataException Corrupt(int index, string problem) =>
            new InvalidDataException("Corrupt RLC file: frame " + index + " " + problem + ".");
    }
}
