using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using GPExtractor;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class GpFileTests
    {
        private static readonly byte[] Dictionary = GpTestFiles.Dictionary;

        private static byte[] BuildFile(params GpTestFiles.Part[][] frames) => GpTestFiles.Build(frames);

        private static GpFramePart SinglePart(GpTestFiles.Part part) => GpFile.Parse(BuildFile(new[] { part })).Frames[0].Parts[0];

        [Test]
        public void ParsesHeaderFramesAndParts()
        {
            var shadow = new GpTestFiles.Part(GpPartType.Shadow, 1, new byte[] { 0x01, 2, 3 }, 0, Array.Empty<byte>(), X: 5, Y: 7, Width: 6);
            var body = new GpTestFiles.Part(GpPartType.Uncompressed, 2, new byte[] { 0x00, 0x01, 0, 2 }, 2, new byte[] { 9, 8 }, X: 1, Y: 2, Width: 3);

            var file = GpFile.Parse(BuildFile(new[] { shadow, body }, new[] { body }));

            file.Dictionary.Should().Equal(Dictionary);
            file.Frames.Should().HaveCount(2);

            var frame = file.Frames[0];
            frame.Parts.Select(p => p.Type).Should().Equal(GpPartType.Shadow, GpPartType.Uncompressed);
            frame.Parts[1].X.Should().Be(1);
            frame.Parts[1].Y.Should().Be(2);
            frame.Parts[1].PixelCount.Should().Be(2);
            frame.Width.Should().Be(11);
            frame.Height.Should().Be(8);
            file.Frames[1].Parts.Should().ContainSingle();
        }

        [Test]
        public void DecodesRowsInAllEncodings()
        {
            var shape = new byte[]
            {
                0x00,                   // empty row
                0x02, 3, 4, 1, 20,      // two runs as byte pairs
                0x81, 0x52,             // one compact run: skip 2, length 5
                0xE1, 0x52,             // + 16 to skip and length
                0x92, 0x11, 0x11,       // bit 0x10 belongs to the run count: 0x12 = 18 runs, not 2
            };
            shape = shape.Concat(Enumerable.Repeat((byte) 0x11, 16)).ToArray();

            var rows = SinglePart(new GpTestFiles.Part(GpPartType.Shadow, 5, shape, 0, Array.Empty<byte>())).DecodeRows();

            rows[0].Should().BeEmpty();
            rows[1].Should().Equal(new GpRun(3, 4), new GpRun(1, 20));
            rows[2].Should().Equal(new GpRun(2, 5));
            rows[3].Should().Equal(new GpRun(18, 21));
            rows[4].Should().HaveCount(18).And.AllBeEquivalentTo(new GpRun(1, 1));
        }

        [Test]
        public void DecodesDictionaryCompressedPixels()
        {
            // Control 0b0100_0000: literal 7; then 0x02 0x10 copies (1 + 3) indices from dictionary offset 2; then literal 8.
            var colors = new byte[] { 0b0100_0000, 7, 0x02, 0x10, 8 };
            var part = new GpTestFiles.Part(GpPartType.Normal, 1, new byte[] { 0x01, 0, 6 }, 6, colors);

            SinglePart(part).DecodePixels(Dictionary).Should().Equal(7, 102, 103, 104, 105, 8);
        }

        [Test]
        public void TruncatesTheLastDictionaryRunAtThePixelCount()
        {
            var colors = new byte[] { 0b1000_0000, 0x00, 0xF0 }; // copy 18 indices
            var part = new GpTestFiles.Part(GpPartType.Normal, 1, new byte[] { 0x01, 0, 5 }, 5, colors);

            SinglePart(part).DecodePixels(Dictionary).Should().Equal(100, 101, 102, 103, 104);
        }

        [Test]
        public void DecodesPlayerColorAsTwoBitsPerPixelLowestBitsFirst()
        {
            var part = new GpTestFiles.Part(GpPartType.PlayerColor, 1, new byte[] { 0x01, 0, 5 }, 5, new byte[] { 0b11_10_01_00, 0b10 });

            SinglePart(part).DecodePixels(Dictionary).Should().Equal(0, 1, 2, 3, 2);
        }

        [TestCase(GpPartType.Blend25)]
        [TestCase(GpPartType.Blend50)]
        [TestCase(GpPartType.Blend75)]
        [TestCase(GpPartType.Translucent)]
        [TestCase(GpPartType.Uncompressed)]
        public void DecodesUncompressedTypes(GpPartType type)
        {
            var part = new GpTestFiles.Part(type, 1, new byte[] { 0x01, 0, 3 }, 3, new byte[] { 11, 12, 13 });

            SinglePart(part).DecodePixels(Dictionary).Should().Equal(11, 12, 13);
        }

        [Test]
        public void ReportsUnsupportedTypes()
        {
            var part = SinglePart(new GpTestFiles.Part(GpPartType.Unknown42, 1, new byte[] { 0x01, 0, 1 }, 1, new byte[] { 1 }));

            part.IsSupported.Should().BeFalse();
            part.Invoking(p => p.DecodePixels(Dictionary)).Should().Throw<NotSupportedException>();
        }

        [Test]
        public void RejectsFilesWithoutSignature()
        {
            Action act = () => GpFile.Parse(new byte[32]);

            act.Should().Throw<InvalidDataException>().WithMessage("*GPAK*");
        }

        [Test]
        public void RejectsTruncatedColorData()
        {
            var bytes = BuildFile(new[] { new GpTestFiles.Part(GpPartType.Uncompressed, 1, new byte[] { 0x01, 0, 3 }, 3, new byte[] { 1, 2, 3 }) });
            Array.Resize(ref bytes, bytes.Length - 1);

            var part = GpFile.Parse(bytes).Frames[0].Parts[0];

            part.Invoking(p => p.DecodePixels(Dictionary)).Should().Throw<InvalidDataException>();
        }
    }
}
