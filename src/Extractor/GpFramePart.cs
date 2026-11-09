using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace GPExtractor
{
    /// <summary>How the color data of a <see cref="GpFramePart"/> is encoded and drawn.</summary>
    public enum GpPartType : byte
    {
        /// <summary>Opaque pixels, compressed against the file's color dictionary.</summary>
        Normal = 0,

        /// <summary>Player (nation) color: 2 bits per pixel, an index into the player's 4 palette entries.</summary>
        PlayerColor = 1,

        /// <summary>Anti-aliased outer edge: one palette index per pixel, mostly transparent.</summary>
        Blend25 = 2,

        /// <summary>Anti-aliased edge: one palette index per pixel, half transparent.</summary>
        Blend50 = 3,

        /// <summary>Anti-aliased inner edge: one palette index per pixel, mostly opaque.</summary>
        Blend75 = 4,

        /// <summary>Shadow: only a shape, no color data.</summary>
        Shadow = 5,

        /// <summary>Translucent effect (smoke, water): one palette index per pixel.</summary>
        Translucent = 7,

        /// <summary>Opaque, uncompressed: one palette index per pixel (interface graphics).</summary>
        Uncompressed = 39,

        /// <summary>Unknown compression, not supported yet.</summary>
        Unknown42 = 42,

        /// <summary>Empty placeholder.</summary>
        Empty = 255,
    }

    /// <summary>A horizontal run of drawn pixels: skip <see cref="Skip"/> pixels, then draw <see cref="Length"/>.</summary>
    public readonly record struct GpRun(int Skip, int Length);

    /// <summary>
    /// One layer of a frame.
    /// </summary>
    /// <remarks>
    /// Header (23 bytes, little-endian):
    /// <code>
    /// +0   int32   offset of the next part, relative to the frame start; -1 for the last part
    /// +4   int16   x, y, width, height
    /// +12  int32   always -1 (filled in by the game at run time)
    /// +16  byte    part type (<see cref="GpPartType"/>)
    /// +17  uint32  bits 0-13: offset of the color data + 1; bits 14-31: number of pixels
    /// +21  uint16  number of rows
    /// </code>
    /// The header is followed by one shape entry per row (see <see cref="DecodeRows"/>),
    /// one unused byte, and the color data (see <see cref="DecodePixels"/>).
    /// </remarks>
    public sealed class GpFramePart
    {
        public const int HeaderSize = 23;

        private readonly byte[] _bytes;
        private readonly int _start;
        private readonly int _colorDataOffset;

        internal GpFramePart(byte[] bytes, int start)
        {
            _bytes = bytes;
            _start = start;

            var header = bytes.AsSpan(start, HeaderSize);
            NextPartOffset = BinaryPrimitives.ReadInt32LittleEndian(header);
            X = BinaryPrimitives.ReadInt16LittleEndian(header.Slice(4));
            Y = BinaryPrimitives.ReadInt16LittleEndian(header.Slice(6));
            Width = BinaryPrimitives.ReadInt16LittleEndian(header.Slice(8));
            Height = BinaryPrimitives.ReadInt16LittleEndian(header.Slice(10));
            Type = (GpPartType) header[16];

            var packed = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(17));
            _colorDataOffset = (int) (packed & 0x3FFF);
            PixelCount = (int) (packed >> 14);
            RowCount = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(21));
        }

        public int NextPartOffset { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public GpPartType Type { get; }

        /// <summary>
        /// Number of pixel values in the color data: the pixels drawn by the shape, 0 for shadows,
        /// and for <see cref="GpPartType.PlayerColor"/> padded with 1-4 values at the end.
        /// </summary>
        public int PixelCount { get; }

        public int RowCount { get; }

        public bool IsSupported => Type != GpPartType.Unknown42 && Enum.IsDefined(Type);

        /// <summary>
        /// Decodes which pixels of each row are drawn.
        /// </summary>
        /// <remarks>
        /// Each row starts with a code byte:
        /// <list type="bullet">
        /// <item><c>0x00</c>: empty row.</item>
        /// <item><c>0x01-0x7F</c>: that many runs follow, two bytes each (skip, length).</item>
        /// <item><c>0x80-0xFF</c>: the low 5 bits are the number of runs, one byte each
        /// (low nibble skip, high nibble length). Bit 0x40 adds 16 to every skip, bit 0x20 adds 16 to every length.</item>
        /// </list>
        /// </remarks>
        public IReadOnlyList<GpRun[]> DecodeRows()
        {
            if (Type == GpPartType.Empty)
            {
                return Array.Empty<GpRun[]>();
            }

            var rows = new GpRun[RowCount][];
            var offset = _start + HeaderSize;

            for (var row = 0; row < RowCount; row++)
            {
                var code = ReadByte(ref offset);

                if (code < 0x80)
                {
                    var runs = new GpRun[code];
                    for (var i = 0; i < code; i++)
                    {
                        var skip = ReadByte(ref offset);
                        runs[i] = new GpRun(skip, ReadByte(ref offset));
                    }

                    rows[row] = runs;
                }
                else
                {
                    var count = code & 0x1F;
                    var skipBase = (code & 0x40) != 0 ? 16 : 0;
                    var lengthBase = (code & 0x20) != 0 ? 16 : 0;

                    var runs = new GpRun[count];
                    for (var i = 0; i < count; i++)
                    {
                        var packed = ReadByte(ref offset);
                        runs[i] = new GpRun(skipBase + (packed & 0x0F), lengthBase + (packed >> 4));
                    }

                    rows[row] = runs;
                }
            }

            return rows;
        }

        /// <summary>
        /// Decodes the color of every drawn pixel, in row order.
        /// </summary>
        /// <returns>
        /// <see cref="PixelCount"/> values: palette indices, or for <see cref="GpPartType.PlayerColor"/> 0-3
        /// (an offset into the player's colors). Empty for shadows and empty parts.
        /// </returns>
        /// <exception cref="NotSupportedException">The part type is not supported.</exception>
        public byte[] DecodePixels(byte[] dictionary)
        {
            switch (Type)
            {
                case GpPartType.Shadow:
                case GpPartType.Empty:
                    return Array.Empty<byte>();

                case GpPartType.Normal:
                    return DecodeDictionaryCompressed(dictionary);

                case GpPartType.PlayerColor:
                    return DecodeTwoBitsPerPixel();

                case GpPartType.Blend25:
                case GpPartType.Blend50:
                case GpPartType.Blend75:
                case GpPartType.Translucent:
                case GpPartType.Uncompressed:
                    return Slice(ColorDataStart, PixelCount).ToArray();

                default:
                    throw new NotSupportedException("GP part type " + (int) Type + " is not supported.");
            }
        }

        // The byte before the color data (at offset - 1) is not used.
        private int ColorDataStart => _start + _colorDataOffset;

        /// <summary>
        /// Groups of a control byte followed by 8 items, read from the control byte's highest bit down.
        /// Bit 0: one literal palette index. Bit 1: two bytes a, b; copy (b &gt;&gt; 4) + 3 indices
        /// from the dictionary, starting at a | (b &amp; 0x0F) &lt;&lt; 8.
        /// </summary>
        private byte[] DecodeDictionaryCompressed(byte[] dictionary)
        {
            var pixels = new byte[PixelCount];
            var count = 0;
            var offset = ColorDataStart;

            while (count < pixels.Length)
            {
                var control = ReadByte(ref offset);

                for (var bit = 7; bit >= 0 && count < pixels.Length; bit--)
                {
                    if ((control >> bit & 1) == 0)
                    {
                        pixels[count++] = ReadByte(ref offset);
                        continue;
                    }

                    var low = ReadByte(ref offset);
                    var high = ReadByte(ref offset);
                    var source = low | (high & 0x0F) << 8;
                    var length = (high >> 4) + 3;

                    if (source + length > dictionary.Length)
                    {
                        throw new InvalidDataException("Corrupt GP file: dictionary reference out of range.");
                    }

                    // The last run may reach past the pixel count.
                    length = Math.Min(length, pixels.Length - count);
                    Array.Copy(dictionary, source, pixels, count, length);
                    count += length;
                }
            }

            return pixels;
        }

        /// <summary>Four pixels per byte, lowest bits first.</summary>
        private byte[] DecodeTwoBitsPerPixel()
        {
            var data = Slice(ColorDataStart, (PixelCount + 3) / 4);
            var pixels = new byte[PixelCount];

            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = (byte) (data[i / 4] >> 2 * (i % 4) & 0x03);
            }

            return pixels;
        }

        private byte ReadByte(ref int offset)
        {
            if (offset >= _bytes.Length)
            {
                throw new InvalidDataException("Corrupt GP file: part data runs past the end of the file.");
            }

            return _bytes[offset++];
        }

        private ReadOnlySpan<byte> Slice(int offset, int length)
        {
            if (offset < 0 || offset + (long) length > _bytes.Length)
            {
                throw new InvalidDataException("Corrupt GP file: part data runs past the end of the file.");
            }

            return _bytes.AsSpan(offset, length);
        }
    }
}
