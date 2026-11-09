using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace GPExtractor
{
    /// <summary>
    /// A GPAK sprite file (<c>*.gp</c>) as used by Cossacks and American Conquest.
    /// </summary>
    /// <remarks>
    /// File layout (little-endian):
    /// <code>
    /// 0x00  "GPAK"
    /// 0x04  uint16  number of frames N
    /// 0x06  uint16  unknown
    /// 0x08  uint32  offset of the shared color dictionary
    /// 0x0C  uint16  length of the dictionary
    /// 0x0E  uint32 * N  frame offsets
    /// </code>
    /// A frame is a chain of parts (see <see cref="GpFramePart"/>) that are drawn on top of each other.
    /// </remarks>
    public sealed class GpFile
    {
        private const int HeaderSize = 0x0E;
        private const int MaxPartsPerFrame = 1024;

        private GpFile(byte[] dictionary, IReadOnlyList<GpFrame> frames)
        {
            Dictionary = dictionary;
            Frames = frames;
        }

        /// <summary>Palette indices that type 0 parts copy runs of pixels from.</summary>
        public byte[] Dictionary { get; }

        public IReadOnlyList<GpFrame> Frames { get; }

        public static GpFile Load(string path)
        {
            return Parse(File.ReadAllBytes(path));
        }

        public static GpFile Parse(byte[] bytes)
        {
            if (bytes.Length < HeaderSize || bytes[0] != 'G' || bytes[1] != 'P' || bytes[2] != 'A' || bytes[3] != 'K')
            {
                throw new InvalidDataException("Not a GP file: missing GPAK signature.");
            }

            var frameCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4));
            var dictionaryOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8));
            var dictionaryLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(12));

            if (HeaderSize + 4L * frameCount > bytes.Length)
            {
                throw new InvalidDataException("Corrupt GP file: frame table does not fit in the file.");
            }

            if (dictionaryOffset + (long) dictionaryLength > bytes.Length)
            {
                throw new InvalidDataException("Corrupt GP file: color dictionary lies outside the file.");
            }

            var dictionary = bytes.AsSpan((int) dictionaryOffset, dictionaryLength).ToArray();

            var frames = new GpFrame[frameCount];
            for (var i = 0; i < frameCount; i++)
            {
                var frameOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(HeaderSize + 4 * i));
                frames[i] = new GpFrame(i, ReadParts(bytes, frameOffset));
            }

            return new GpFile(dictionary, frames);
        }

        private static IReadOnlyList<GpFramePart> ReadParts(byte[] bytes, uint frameOffset)
        {
            var parts = new List<GpFramePart>();

            // Some files end with frame offsets that point at the end of the file: empty frames.
            var offset = (long) frameOffset;
            while (offset + GpFramePart.HeaderSize <= bytes.Length)
            {
                var part = new GpFramePart(bytes, (int) offset);
                parts.Add(part);

                if (part.NextPartOffset == -1)
                {
                    break;
                }

                if (parts.Count >= MaxPartsPerFrame)
                {
                    throw new InvalidDataException("Corrupt GP file: too many parts in a frame.");
                }

                offset = frameOffset + (long) part.NextPartOffset;
            }

            return parts;
        }
    }

    public sealed class GpFrame
    {
        internal GpFrame(int index, IReadOnlyList<GpFramePart> parts)
        {
            Index = index;
            Parts = parts;
        }

        public int Index { get; }

        /// <summary>Parts in drawing order (later parts are drawn on top).</summary>
        public IReadOnlyList<GpFramePart> Parts { get; }

        /// <summary>Width of the area covered by the parts, measured from x = 0.</summary>
        public int Width
        {
            get
            {
                var width = 0;
                foreach (var part in Parts)
                {
                    width = Math.Max(width, part.X + part.Width);
                }

                return width;
            }
        }

        /// <summary>Height of the area covered by the parts, measured from y = 0.</summary>
        public int Height
        {
            get
            {
                var height = 0;
                foreach (var part in Parts)
                {
                    height = Math.Max(height, part.Y + part.Height);
                }

                return height;
            }
        }
    }
}
