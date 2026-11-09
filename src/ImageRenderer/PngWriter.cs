using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using Types;

namespace ImageRenderer
{
    /// <summary>
    /// Minimal, dependency-free PNG encoder (8-bit RGBA, no filtering).
    /// </summary>
    public static class PngWriter
    {
        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public static void Save(ImageView image, string path)
        {
            using (var stream = File.Create(path))
            {
                Save(image, stream);
            }
        }

        public static void Save(ImageView image, Stream stream)
        {
            stream.Write(Signature, 0, Signature.Length);

            var header = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), image.Width);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height);
            header[8] = 8;  // bit depth
            header[9] = 6;  // color type: RGBA
            header[10] = 0; // compression: deflate
            header[11] = 0; // filter method
            header[12] = 0; // no interlace
            WriteChunk(stream, "IHDR", header);

            WriteChunk(stream, "IDAT", Compress(image));
            WriteChunk(stream, "IEND", Array.Empty<byte>());
        }

        private static byte[] Compress(ImageView image)
        {
            var stride = image.Width * 4;
            var row = new byte[stride + 1]; // leading filter-type byte (0 = None)

            using (var output = new MemoryStream())
            {
                using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
                {
                    for (int y = 0; y < image.Height; y++)
                    {
                        for (int x = 0; x < image.Width; x++)
                        {
                            var color = image.Pixels[y * image.Width + x];
                            var i = 1 + x * 4;
                            row[i] = color.R;
                            row[i + 1] = color.G;
                            row[i + 2] = color.B;
                            row[i + 3] = color.A;
                        }

                        zlib.Write(row, 0, row.Length);
                    }
                }

                return output.ToArray();
            }
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            var typeBytes = Encoding.ASCII.GetBytes(type);
            var buffer = new byte[4];

            BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
            stream.Write(buffer, 0, 4);
            stream.Write(typeBytes, 0, 4);
            stream.Write(data, 0, data.Length);

            var crc = Crc32.Update(Crc32.Update(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
            BinaryPrimitives.WriteUInt32BigEndian(buffer, crc);
            stream.Write(buffer, 0, 4);
        }

        private static class Crc32
        {
            private static readonly uint[] Table = CreateTable();

            private static uint[] CreateTable()
            {
                var table = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    var c = n;
                    for (int k = 0; k < 8; k++)
                    {
                        c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    }
                    table[n] = c;
                }
                return table;
            }

            public static uint Update(uint crc, byte[] data)
            {
                foreach (var b in data)
                {
                    crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
                }
                return crc;
            }
        }
    }
}
