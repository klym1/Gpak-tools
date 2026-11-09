using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using GPExtractor;
using ImageRenderer;
using NUnit.Framework;

namespace UnitTests
{
    /// <summary>
    /// Renders every image in gp/test.gp and compares it with reference hashes.
    /// The references were produced by the original GDI+ (System.Drawing.Bitmap)
    /// pipeline, so these tests guard pixel-exact output on every platform.
    /// </summary>
    [TestFixture]
    public class RenderingTests
    {
        private static readonly string[] ExpectedImages =
        {
            "576x467:21B0FE1991803055568737669D26893E4794086B84980AE1F782A063C0ADC10A",
            "570x364:9A1DC327397ACAC43B5B2BA10A139BF699259D2B433255F743FE4500F4D19861",
            "576x467:BBE68A602DD9703B537C8139450E2284D0A977316FC7C77C13A1CE7BAFF889CA",
            "570x364:777835D22733CC2DC4C1F2A33D0FC63FF1764A4748B42BA11DBD1F93DCD683D2",
            "576x467:7739412495339E7C8B5ECACC69B963B9ED438397E325280A3C6FAC218F7F8982",
            "570x364:0AC63316B9DA93F5439424E780461E9CB9B8BC12CAC33C5FDBDD2081F0C77ADB",
            "576x467:1C9B70347730851C6D738BD99BA41B3B29CBFDDB699EE9BF1482D7AD72B49FA1",
            "570x364:55419B3A4F72D3415CCFF48E875FDB49D421637D35C7B7F6250765DF4ABA2EBB",
            "563x466:F4253849E82D662340CE5756A8E75533F0B830DE25C9CA3643A714CAFD4E248C",
            "453x435:F1B9A84A8BD35121FC0E817B8000DCF4F0549E818A3852EC68D5A5CF5D4E2B85",
            "544x385:3CC2641D740CE83F40D1192DF9BB34C6F95A45ED78DE80CEE4CA8B77BCE7A4EC",
            "563x466:77C95EE49218C903096F2504BB5727D9F59ED08E2CB7DBD4AC2F791F4AC57210",
            "544x352:331BEF2D7FB93A0AB0A623D347058419BF4E0CE5F42564C5FC3D0F589ED4F63E",
            "563x466:12E01C69FBF02AE078054065B83492869B12514A6FE2231BF9C402E2760F1D26",
            "544x352:BD22A3928FAABA705F70B72B33C9A356C895160A0EB4DC85607B3A180B12E006",
        };

        [Test]
        public void RenderedImagesMatchReference()
        {
            var gpFile = Path.Combine(AppContext.BaseDirectory, "gp", "test.gp");
            var paletteFile = Path.Combine(AppContext.BaseDirectory, "palette", "0", "agew_1.pal");

            var imagePaletteBytes = new Extractor().GetPaletteBytes(gpFile);
            var layouts = new Extractor().GetImagesFromOutput(gpFile).ToList();
            var rawParser = new RawParser();
            var colors = rawParser.GetColorCollectionFromPalleteFile(File.ReadAllBytes(paletteFile));
            var imagePaletteColors = ImageGenerator.OffsetsToColors(imagePaletteBytes, colors);

            var actual = Enumerable.Range(0, layouts.Count)
                .Select(i => new Runner().Run(layouts, NationColorOffset.Blue, i, rawParser, new BitmapRenderer(), imagePaletteColors, colors))
                .Select(image =>
                {
                    var bytes = image.Pixels.SelectMany(color => BitConverter.GetBytes(color.ToArgb())).ToArray();
                    return image.Width + "x" + image.Height + ":" + Convert.ToHexString(SHA256.HashData(bytes));
                })
                .ToArray();

            Assert.That(actual, Is.EqualTo(ExpectedImages));
        }
    }
}
