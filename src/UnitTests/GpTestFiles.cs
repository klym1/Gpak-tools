using System.IO;
using System.Linq;
using GPExtractor;

namespace UnitTests
{
    /// <summary>
    /// Builds synthetic GP files in the layout of the game files, so tests need no game data.
    /// Also compiled into ViewerTests.
    /// </summary>
    public static class GpTestFiles
    {
        public static readonly byte[] Dictionary = Enumerable.Range(100, 32).Select(i => (byte) i).ToArray();

        public sealed record Part(GpPartType Type, int Rows, byte[] Shape, int PixelCount, byte[] Colors, int X = 0, int Y = 0, int Width = 40);

        /// <summary>A one-row part drawing <paramref name="length"/> uncompressed pixels of one color at (x, y).</summary>
        public static Part Line(int x, int y, int length, byte color = 10) =>
            new Part(GpPartType.Translucent, 1, new byte[] { 0x01, 0, (byte) length }, length,
                Enumerable.Repeat(color, length).ToArray(), X: x, Y: y, Width: length);

        /// <summary>Builds a GP file with one frame per part list.</summary>
        public static byte[] Build(params Part[][] frames)
        {
            var output = new MemoryStream();
            var writer = new BinaryWriter(output);

            var tableEnd = 0x0E + 4 * frames.Length;
            writer.Write("GPAK"u8.ToArray());
            writer.Write((ushort) frames.Length);
            writer.Write((ushort) 0);
            writer.Write((uint) tableEnd);
            writer.Write((ushort) Dictionary.Length);

            var frameBytes = frames.Select(BuildFrame).ToList();
            var offset = tableEnd + Dictionary.Length;
            foreach (var bytes in frameBytes)
            {
                writer.Write((uint) offset);
                offset += bytes.Length;
            }

            writer.Write(Dictionary);
            frameBytes.ForEach(writer.Write);
            return output.ToArray();
        }

        /// <summary>
        /// A unit sprite with 9 stored directions and the given number of poses. Frame (pose, direction) draws
        /// a line from x = anchor to the right, direction + 1 pixels long, in palette color 10 + pose, so the
        /// direction, the pose and mirroring can be read back from the image.
        /// </summary>
        public static byte[] Unit(int poses, int anchorX)
        {
            var frames = Enumerable.Range(0, poses * 9)
                .Select(frame => new[] { Line(anchorX, 0, frame % 9 + 1, (byte) (10 + frame / 9)) })
                .ToArray();
            return Build(frames);
        }

        private static byte[] BuildFrame(Part[] parts)
        {
            var output = new MemoryStream();
            var writer = new BinaryWriter(output);

            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var size = GpFramePart.HeaderSize + part.Shape.Length + 1 + part.Colors.Length;
                var start = (int) output.Length;

                writer.Write(i == parts.Length - 1 ? -1 : start + size);
                writer.Write((short) part.X);
                writer.Write((short) part.Y);
                writer.Write((short) part.Width);
                writer.Write((short) part.Rows);
                writer.Write(-1);
                writer.Write((byte) part.Type);
                writer.Write((uint) (GpFramePart.HeaderSize + part.Shape.Length + 1) | (uint) part.PixelCount << 14);
                writer.Write((ushort) part.Rows);
                writer.Write(part.Shape);
                writer.Write((byte) 0xCD);
                writer.Write(part.Colors);
            }

            return output.ToArray();
        }
    }
}
