using System.IO;
using System.Linq;
using System.Text;
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

        /// <summary>Builds a .gsc archive in the layout of Cossacks' All.gsc.</summary>
        public static byte[] Archive(params (string Name, byte[] Content, bool Obfuscate)[] files)
        {
            var output = new MemoryStream();
            var writer = new BinaryWriter(output);

            writer.Write(Encoding.ASCII.GetBytes("GSCfmt"));
            writer.Write(new byte[] { 0x00, 0x02, 0xCD, 0x78 });
            writer.Write((uint) files.Length);

            uint offset = 0;
            foreach (var file in files)
            {
                writer.Write(new byte[] { 0x95, 0x7A, 0x5E, 0xC1 });
                var name = new byte[64];
                Encoding.ASCII.GetBytes(file.Name).CopyTo(name, 0);
                writer.Write(name);
                writer.Write(~offset);
                writer.Write((uint) file.Content.Length);
                writer.Write(0u);
                writer.Write((byte) 1);
                offset += (uint) file.Content.Length;
            }

            foreach (var file in files)
            {
                writer.Write(file.Obfuscate ? file.Content.Select(b => (byte) (b ^ GscArchive.XorKey)).ToArray() : file.Content);
            }

            return output.ToArray();
        }

        /// <summary>An archive like the game's: everything but GP sprites obfuscated.</summary>
        public static byte[] GameArchive(params (string Name, byte[] Content)[] files) =>
            Archive(files.Select(file => (file.Name, file.Content, !file.Name.EndsWith(".GP", System.StringComparison.OrdinalIgnoreCase))).ToArray());

        /// <summary>
        /// Game data for one unit, "TST", and one building, "DEPO", like the game's: a unit descriptor in DOS Cyrillic and two
        /// sprites with different anchors. The stand sprite (tsts.gp, 1 pose) is anchored at (20, 10), the walk
        /// sprite (tstg.gp, 4 poses) at (30, 15); both draw a line starting at the anchor (see <see cref="Unit"/>).
        /// </summary>
        public static (string Name, byte[] Content)[] UnitGameData()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var descriptor = Encoding.GetEncoding(866).GetBytes(string.Join("\r\n",
                "NAME Testovyj",
                "MESSAGE Тестовый_воин",
                "USERLC 0 tsts  SHADOW -20 -10",
                "USERLC 1 tstg  SHADOW -30 -15",
                "#STAND     9 1   0 0",
                "@MOTION_L  9 1 0 2",
                "@MOTION_R  9 1 2 4",
                "@MOTION_LB 9 1 3 1",
                "LIFE 50",
                ""));

            var building = Encoding.ASCII.GetBytes(string.Join("\r\n",
                "NAME Depo",
                "USERLC 0 tsts",
                "#STANDLO 1 1 0 0",
                ""));

            return new[]
            {
                ("TST.MD", descriptor),
                ("TSTS.GP", UnitAt(poses: 1, anchorX: 20, anchorY: 10)),
                ("TSTG.GP", UnitAt(poses: 4, anchorX: 30, anchorY: 15)),
                ("DEPO.MD", building),
            };
        }

        /// <summary>Like <see cref="Unit"/>, with the line in row <paramref name="anchorY"/>.</summary>
        public static byte[] UnitAt(int poses, int anchorX, int anchorY)
        {
            var frames = Enumerable.Range(0, poses * 9)
                .Select(frame => new[] { Line(anchorX, anchorY, frame % 9 + 1, (byte) (10 + frame / 9)) })
                .ToArray();
            return Build(frames);
        }

        /// <summary>An RLC file whose frames each draw one pixel of a color at (x, y), the bottom right corner.</summary>
        public static byte[] RlcOfDots(int frames, int x, int y, byte color)
        {
            var output = new MemoryStream();
            var writer = new BinaryWriter(output);
            var frameSize = 4 + y + 4;

            writer.Write(new byte[] { (byte) 'R', (byte) 'L', (byte) 'C', 0x1B });
            writer.Write((uint) frames);
            for (var i = 0; i < frames; i++)
            {
                writer.Write((uint) (8 + 4 * frames + i * frameSize));
            }

            for (var i = 0; i < frames; i++)
            {
                writer.Write((ushort) (x + 1));
                writer.Write((ushort) (y + 1));
                writer.Write(new byte[y]);
                writer.Write(new byte[] { 1, (byte) x, 1, color });
            }

            return output.ToArray();
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
