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
