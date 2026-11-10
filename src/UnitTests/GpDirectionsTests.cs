using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using GPExtractor;
using ImageRenderer;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class GpDirectionsTests
    {
        private const string Descriptor =
            "NAME Barabanshik_1\r\n" +
            "USERLC 0 ba1s  SHADOW -62 -90\r\n" +
            "USERLC 1 ba1g  SHADOW -62 -90\r\n" +
            "#STAND     9 1   0 0\r\n" +
            "#MOTION_L  9 3  1 0 1 1 1 2\r\n" +
            "#MOTION_R  9 3  1 3 1 4 1 5\r\n" +
            "LIFE     50\r\n";

        [Test]
        public void ParsesSpritesAndAnimationsOfADescriptor()
        {
            var descriptor = GpDescriptor.Parse("BA1", Descriptor);

            descriptor.Sprites.Should().Equal(
                new GpDescriptorSprite(0, "ba1s", 62, 90),
                new GpDescriptorSprite(1, "ba1g", 62, 90));
            descriptor.Animations.Select(a => a.Name).Should().Equal("STAND", "MOTION_L", "MOTION_R");
            descriptor.Animations[1].Directions.Should().Be(9);
            descriptor.Animations[1].Frames.Should().Equal((1, 0), (1, 1), (1, 2));
        }

        [Test]
        public void MapsDirectionsToStoredAndMirroredFrames()
        {
            var directions = new GpDirections(9, 20, 62, GpDirectionsSource.Descriptor);

            directions.Count.Should().Be(16);
            directions.IsMirrored.Should().BeTrue();

            // Stored: south (0) clockwise over west (4) to north (8).
            directions.FrameOf(0, 0).Should().Be((0, false));
            directions.FrameOf(3, 4).Should().Be((31, false));
            directions.FrameOf(0, 8).Should().Be((8, false));
            // Mirrored: east (12) is west flipped, south-southeast (15) is south-southwest flipped.
            directions.FrameOf(3, 12).Should().Be((31, true));
            directions.FrameOf(0, 15).Should().Be((1, true));

            directions.NameOf(0).Should().Be("S");
            directions.NameOf(4).Should().Be("W");
            directions.NameOf(12).Should().Be("E");
            directions.NameOf(15).Should().Be("SSE");
        }

        [Test]
        public void NamesFinerDirectionsInDegrees()
        {
            var directions = new GpDirections(65, 1, 0, GpDirectionsSource.Descriptor);

            directions.Count.Should().Be(128);
            directions.NameOf(1).Should().Be("2.8°");
            directions.NameOf(64).Should().Be("180°");
        }

        [Test]
        public void DoesNotMirrorOtherDirectionCounts()
        {
            var directions = new GpDirections(2, 3, 0, GpDirectionsSource.Descriptor);

            directions.Count.Should().Be(2);
            directions.FrameOf(1, 1).Should().Be((3, false));
        }

        [Test]
        public void TakesDirectionsAndAnchorFromADescriptor()
        {
            var file = GpFile.Parse(GpTestFiles.Unit(poses: 6, anchorX: 62));

            var directions = GpDirections.FromDescriptors("BA1G", file, new[] { GpDescriptor.Parse("BA1", Descriptor) });

            directions.StoredDirections.Should().Be(9);
            directions.Poses.Should().Be(6);
            directions.AnchorX.Should().Be(62);
            directions.Source.Should().Be(GpDirectionsSource.Descriptor);
            directions.DescriptorName.Should().Be("BA1");
        }

        [Test]
        public void IgnoresDescriptorsOfOtherSprites()
        {
            var file = GpFile.Parse(GpTestFiles.Unit(poses: 6, anchorX: 62));

            GpDirections.FromDescriptors("OTHER", file, new[] { GpDescriptor.Parse("BA1", Descriptor) }).Should().BeNull();
        }

        [Test]
        public void RecognizesDirectionsFromTheFrames()
        {
            // Within a pose the outline changes with the direction; between poses it stays the same.
            var file = GpFile.Parse(GpTestFiles.Unit(poses: 4, anchorX: 30));

            var directions = GpDirections.FromFrames(file);

            directions.Should().NotBeNull();
            directions.StoredDirections.Should().Be(9);
            directions.Poses.Should().Be(4);
            directions.Source.Should().Be(GpDirectionsSource.FrameAnalysis);
        }

        [Test]
        public void FindsNoDirectionsInAnimationsWithoutThem()
        {
            // 18 frames of one growing line: consecutive frames are alike, so this is a plain animation.
            var frames = Enumerable.Range(0, 18).Select(i => new[] { GpTestFiles.Line(0, 0, i + 1) }).ToArray();
            GpDirections.FromFrames(GpFile.Parse(GpTestFiles.Build(frames))).Should().BeNull();

            var testFile = GpFile.Load(Path.Combine(AppContext.BaseDirectory, "gp", "test.gp"));
            GpDirections.FromFrames(testFile).Should().BeNull();
        }

        [Test]
        public void DetectReadsDescriptorsNextToTheSprite()
        {
            var folder = Path.Combine(Path.GetTempPath(), "GpDirectionsTests-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);

            try
            {
                var sprite = Path.Combine(folder, "BA1G.GP");
                File.WriteAllBytes(sprite, GpTestFiles.Unit(poses: 6, anchorX: 40));
                File.WriteAllText(Path.Combine(folder, "BA1.MD"), Descriptor);

                var directions = GpDirections.Detect(sprite, GpFile.Load(sprite));

                directions.Source.Should().Be(GpDirectionsSource.Descriptor);
                directions.AnchorX.Should().Be(62, "the descriptor's anchor wins over the estimate");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void RendererMirrorsAroundTheCanvasCentre()
        {
            var file = GpFile.Parse(GpTestFiles.Build(new[] { GpTestFiles.Line(5, 0, 3) }));
            var renderer = new GpRenderer(Palette.Parse(new byte[768]));

            // Anchor at x = 5, canvas centred on it: 10 pixels wide, offset 0.
            var plain = renderer.Render(file, file.Frames[0], 10, 1).Image;
            var mirrored = renderer.Render(file, file.Frames[0], 10, 1, mirror: true).Image;

            Enumerable.Range(0, 10).Where(x => plain.Pixels[x].A > 0).Should().Equal(5, 6, 7);
            Enumerable.Range(0, 10).Where(x => mirrored.Pixels[x].A > 0).Should().Equal(2, 3, 4);
        }
    }
}
