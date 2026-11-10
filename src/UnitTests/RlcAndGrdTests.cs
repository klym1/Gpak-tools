using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using GPExtractor;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class RlcAndGrdTests
    {
        // Two frames: 4x3 with runs in rows 0 and 2, and an empty 1x1.
        private static byte[] Rlc()
        {
            var output = new MemoryStream();
            var writer = new BinaryWriter(output);
            writer.Write(new byte[] { (byte) 'R', (byte) 'L', (byte) 'C', 0x1B });
            writer.Write(2u);
            writer.Write(16u);
            writer.Write(33u);

            writer.Write((ushort) 4);
            writer.Write((ushort) 3);
            writer.Write(new byte[] { 2, 0, 1, 10, 1, 2, 20, 21 });
            writer.Write((byte) 0);
            writer.Write(new byte[] { 1, 3, 1, 30 });

            writer.Write((ushort) 1);
            writer.Write((ushort) 1);
            writer.Write((byte) 0);
            return output.ToArray();
        }

        [Test]
        public void ParsesRlcFramesAndRuns()
        {
            var file = RlcFile.Parse(Rlc());

            file.Frames.Should().HaveCount(2);
            var frame = file.Frames[0];
            (frame.Width, frame.Height).Should().Be((4, 3));
            frame.Rows[0].Select(run => run.X).Should().Equal(0, 2);
            frame.Rows[0][1].Pixels.Should().Equal(20, 21);
            frame.Rows[1].Should().BeEmpty();

            frame.DecodePixels().Should().Equal(
                10, -1, 20, 21,
                -1, -1, -1, -1,
                -1, -1, -1, 30);
            file.Frames[1].DecodePixels().Should().Equal(-1);
        }

        [Test]
        public void RejectsBrokenRlcFiles()
        {
            var bytes = Rlc();

            Action wrongSignature = () => RlcFile.Parse(new byte[] { 1, 2, 3, 4, 0, 0, 0, 0 });
            Action truncated = () => RlcFile.Parse(bytes.Take(20).ToArray());
            var tooWide = (byte[]) bytes.Clone();
            tooWide[30] = 9;
            Action wideRun = () => RlcFile.Parse(tooWide);

            wrongSignature.Should().Throw<InvalidDataException>();
            truncated.Should().Throw<InvalidDataException>();
            wideRun.Should().Throw<InvalidDataException>().WithMessage("*wider than the frame*");
        }

        [Test]
        public void RendersRlcFramesInThePlayerColor()
        {
            var colors = new byte[768];
            for (var i = 0; i < 256; i++)
            {
                colors[3 * i] = (byte) i;
            }

            var red = NationColorOffset.Red.Offset;
            var blue = NationColorOffset.Blue.Offset;
            var file = RlcFile.Parse(GpTestFiles.RlcOfDots(frames: 1, x: 2, y: 1, color: (byte) (red + 1)));
            var paletteFile = Path.Combine(Path.GetTempPath(), "rlc-test-" + Guid.NewGuid() + ".pal");
            File.WriteAllBytes(paletteFile, colors);
            var palette = ImageRenderer.Palette.Load(paletteFile);
            File.Delete(paletteFile);
            var renderer = new ImageRenderer.GpRenderer(palette) { PlayerColor = NationColorOffset.Blue };

            var image = renderer.Render(
                  file.Frames[0]
                , 6
                , 2
                , 0
                , true
                , 0
            ).Image;

            (image.Width, image.Height).Should().Be((6, 2));
            var drawn = Enumerable.Range(0, 12).Where(i => image.Pixels[i].A != 0).ToList();
            drawn.Should().Equal(new[] { 6 + 3 }, "mirrored around the centre of the canvas");
            image.Pixels[9].R.Should().Be((byte) (blue + 1));
        }

        [Test]
        public void DrawsFogMoreOpaqueTheBrighterThePixel()
        {
            // Palette: color i is the gray i.
            var colors = Enumerable.Range(0, 256).SelectMany(i => new[] { (byte) i, (byte) i, (byte) i }).ToArray();
            var paletteFile = Path.Combine(Path.GetTempPath(), "fog-test-" + Guid.NewGuid() + ".pal");
            File.WriteAllBytes(paletteFile, colors);
            var palette = ImageRenderer.Palette.Load(paletteFile);
            File.Delete(paletteFile);

            var file = GpFile.Parse(GpTestFiles.Build(new[]
            {
                GpTestFiles.Line(0, 0, 1, 255),
                GpTestFiles.Line(1, 0, 1, 128),
                GpTestFiles.Line(2, 0, 1, 7),
            }));
            var fog = System.Drawing.Color.FromArgb(255, 180, 180, 170);
            var renderer = new ImageRenderer.GpRenderer(palette) { Fog = fog };

            var image = renderer.Render(file, file.Frames[0]).Image;

            image.Pixels.Take(3).Select(pixel => (int) pixel.A).Should().Equal(255, 16 * 255 / 31, 0);
            image.Pixels.Take(2).Select(pixel => (pixel.R, pixel.G, pixel.B)).Should().AllBeEquivalentTo((fog.R, fog.G, fog.B));
        }

        [Test]
        public void ReadsGrdRows()
        {
            var bytes = Enumerable.Range(0, 512).Select(i => (byte) (i < 256 ? i : 255 - i % 256)).ToArray();

            var table = GrdTable.Parse(bytes);

            table.Rows.Should().Be(2);
            table[0, 7].Should().Be(7);
            table[1, 7].Should().Be(248);
            table.Row(1).ToArray().Should().Equal(bytes.Skip(256));
        }

        [Test]
        public void RejectsGrdTablesOfOddSize()
        {
            Action parse = () => GrdTable.Parse(new byte[300]);
            Action row = () => _ = GrdTable.Parse(new byte[256])[1, 0];

            parse.Should().Throw<InvalidDataException>();
            row.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
