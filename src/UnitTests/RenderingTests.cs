using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FluentAssertions;
using GPExtractor;
using ImageRenderer;
using NUnit.Framework;
using Types;

namespace UnitTests
{
    /// <summary>
    /// Decodes and renders every frame of gp/test.gp. The reference hashes guard against
    /// unintended changes; they come from this decoder, checked visually against the game's look.
    /// </summary>
    [TestFixture]
    public class RenderingTests
    {
        private static string TestFile => Path.Combine(AppContext.BaseDirectory, "gp", "test.gp");
        private static string PaletteFile => Path.Combine(AppContext.BaseDirectory, "palette", "0", "agew_1.pal");

        private static readonly string[] ExpectedImages =
        {
            "576x467:759E7C9B11BF0C4B75D0CE7842C9B08C2544FC08C75F38697D63DEDAD95FC3BB",
            "570x364:533FCB67F29347783C5BE6F7F9BB7F636387F8041F4E55DD4F39F520B8F7779E",
            "576x467:A70116B912FB1576C17ACE8AF41C975820015E5677D13CEEF92F6A6C013B309A",
            "570x364:AAA16A2F4C324B7D4CFC3CC1C9212F8EF6A70053959AABE37611020D4CD439D1",
            "576x467:C19A341FB77BDCDC9652B626E072138490CC60118AA573B8649A00A5A0B263EE",
            "570x364:BF636B927347EE57EAC62C93CC42F7B788F55953332A934D3287A5AA0CB56C30",
            "576x467:171B04C759D2257F603448A7EB6E47BAF610360E43D4E7BFD640DDC977750F92",
            "570x364:1DAFE1D54D61C13A901EBD534A6E6E50487044DEDAD45AA3881EB62F2430FE0A",
            "563x466:EE0F712A62CA581AF717BDD2CE9F34876311A729126C114C582DD633E1302454",
            "453x435:A31E5940DEC6D97E350A345132B51A5B063B33EE328564F9504A1FAEF6118CBC",
            "544x385:54ABC675EFC4CF4BC65DC9928F0BB6D475029EE38398A92F3E593326CDC8E711",
            "563x466:385E70ACACA3FA60ABCD2345BE71E5815D9556C20DB10C24532740369CA3B2DC",
            "544x352:EF7FC148D87BCF9DA91434638C43160963124CD65AB375FF28B8AA889848E1AB",
            "563x466:E32BB54AAD8785643B24F87513A5BE20021882E044C089DF54912F83A6E74235",
            "544x352:44D625B18C518F851279906D858EE96FD3FB44FA8A4307BA1895FBFAD72225FB",
        };

        [Test]
        public void EveryPartDecodesToItsPixelCount()
        {
            var file = GpFile.Load(TestFile);

            file.Frames.Should().HaveCount(15);

            foreach (var part in file.Frames.SelectMany(frame => frame.Parts))
            {
                // Shadows have a shape but no color data, so their pixel count is 0.
                if (part.Type == GpPartType.Shadow)
                {
                    part.PixelCount.Should().Be(0);
                    continue;
                }

                // Player color data is padded with 1-4 pixels at the end.
                var shapePixels = part.DecodeRows().SelectMany(row => row).Sum(run => run.Length);
                var expected = part.Type == GpPartType.PlayerColor ? shapePixels / 4 * 4 + 4 : shapePixels;
                part.PixelCount.Should().Be(expected);
                part.DecodePixels(file.Dictionary).Should().HaveCount(part.PixelCount);
            }
        }

        [Test]
        public void RenderedImagesMatchReference()
        {
            var file = GpFile.Load(TestFile);
            var renderer = new GpRenderer(Palette.Load(PaletteFile)) { PlayerColor = NationColorOffset.Blue };

            var actual = file.Frames
                .Select(frame => renderer.Render(file, frame))
                .Select(rendered =>
                {
                    rendered.SkippedParts.Should().BeEmpty();
                    return HashOf(rendered.Image);
                })
                .ToArray();

            Assert.That(actual, Is.EqualTo(ExpectedImages));
        }

        [Test]
        public void RendersPartsInOrderWithShadowAndPlayerColor()
        {
            var file = GpFile.Load(TestFile);
            var palette = Palette.Load(PaletteFile);
            var renderer = new GpRenderer(palette) { PlayerColor = NationColorOffset.Blue };

            var image = renderer.Render(file, file.Frames[0]).Image;

            // Transparent outside the parts, translucent black where only the shadow is drawn.
            image.Pixels[0].A.Should().Be(0);
            image.Pixels.Should().Contain(Color.FromArgb(0x99, 0, 0, 0));
        }

        [Test]
        public void RendersOnALargerCanvasWithoutMovingTheFrame()
        {
            var file = GpFile.Load(TestFile);
            var renderer = new GpRenderer(Palette.Load(PaletteFile));
            var frame = file.Frames[9];

            var own = renderer.Render(file, frame).Image;
            var large = renderer.Render(file, frame, 600, 500).Image;

            large.Width.Should().Be(600);
            large.Height.Should().Be(500);
            for (var y = 0; y < own.Height; y++)
            {
                large.Pixels.AsSpan(y * large.Width, own.Width).ToArray()
                    .Should().Equal(own.Pixels.AsSpan(y * own.Width, own.Width).ToArray());
            }
        }

        internal static string HashOf(ImageView image)
        {
            var bytes = image.Pixels.SelectMany(color => BitConverter.GetBytes(color.ToArgb())).ToArray();
            return image.Width + "x" + image.Height + ":" + Convert.ToHexString(SHA256.HashData(bytes));
        }
    }
}
