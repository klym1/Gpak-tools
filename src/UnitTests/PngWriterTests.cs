using System;
using System.Buffers.Binary;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FluentAssertions;
using ImageRenderer;
using NUnit.Framework;
using Types;

namespace UnitTests
{
    [TestFixture]
    public class PngWriterTests
    {
        [Test]
        public void PngWriterProducesValidRgbaPng()
        {
            var image = new ImageView(3, 2);
            image.Pixels[0] = Color.FromArgb(255, 255, 0, 0);
            image.Pixels[1] = Color.FromArgb(255, 0, 255, 0);
            image.Pixels[2] = Color.FromArgb(255, 0, 0, 255);
            image.Pixels[3] = Color.FromArgb(0x99, 0, 0, 0);
            image.Pixels[4] = Color.FromArgb(255, 0x80, 0x80, 0x80);
            image.Pixels[5] = Color.FromArgb(0, 0, 0, 0);

            byte[] png;
            using (var stream = new MemoryStream())
            {
                PngWriter.Save(image, stream);
                png = stream.ToArray();
            }

            png.Take(8).Should().Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);

            var chunks = ReadChunks(png);
            chunks.Select(c => c.Type).Should().Equal("IHDR", "IDAT", "IEND");

            var header = chunks[0].Data;
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0)).Should().Be(3);
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4)).Should().Be(2);
            header[8].Should().Be(8);  // bit depth
            header[9].Should().Be(6);  // RGBA

            byte[] raw;
            using (var zlib = new ZLibStream(new MemoryStream(chunks[1].Data), CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                zlib.CopyTo(output);
                raw = output.ToArray();
            }

            raw.Should().Equal(
                0, 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255,
                0, 0, 0, 0, 0x99, 0x80, 0x80, 0x80, 255, 0, 0, 0, 0);
        }

        private static (string Type, byte[] Data)[] ReadChunks(byte[] png)
        {
            var result = new System.Collections.Generic.List<(string, byte[])>();
            var offset = 8;

            while (offset < png.Length)
            {
                var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset));
                var type = Encoding.ASCII.GetString(png, offset + 4, 4);
                var data = png.AsSpan(offset + 8, length).ToArray();
                var storedCrc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset + 8 + length));

                storedCrc.Should().Be(Crc32(png.AsSpan(offset + 4, 4 + length)), "CRC of chunk " + type);

                result.Add((type, data));
                offset += 12 + length;
            }

            return result.ToArray();
        }

        // Independent bitwise CRC-32 implementation to cross-check the writer.
        private static uint Crc32(ReadOnlySpan<byte> data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in data)
            {
                crc ^= b;
                for (int k = 0; k < 8; k++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
                }
            }
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
