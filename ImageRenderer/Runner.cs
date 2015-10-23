using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GPExtractor;
using Types;

namespace ImageRenderer
{
    public class Runner
    {
        public Bitmap Run(IList<ImageLayout> extractResult, int imageNumber, RawParser rawParser, IRenderer renderer, Color[] imagePaletteArray, Color[] generalPaletteColors)
        {
            var imageGenerator = new ImageGenerator();

            var partialLayouts = extractResult[imageNumber].PartialLayouts;

            var largestWidth = partialLayouts
                .Select(it => it.Width + it.offsetX)
                .OrderByDescending(it => it)
                .First();

            var largestHeight = partialLayouts
                .Select(it => it.Height + it.offsetY)
                .OrderByDescending(it => it)
                .First();

            var bitMap = new Bitmap(largestWidth, largestHeight);
            renderer.SetupCanvas(bitMap);
            
            foreach (var layout in partialLayouts)
            {
                int offset = 0;

                ImageLayoutInfo layout1 = layout;

                var imageView = new ImageView(layout1.Width + layout1.offsetX, layout1.Height + layout1.offsetY);

                var firstPartBlocks = Helper.WithMeasurement(() =>
                {
                    var rawFirstPartBlocks = rawParser.ParseRawBlockGroups(layout1.Bytes, layout1.NumberOfRows, out offset);
                    return rawFirstPartBlocks.ConvertToAbsoluteCoordinatesBlocks();

                }, "firstPartBlocks");

                var nationColorPart = layout1.HeaderBytes[16] == 1;

                var secondPartBlocks = Helper.WithMeasurement(() => rawParser.GetRawColorBlocks(layout1.Bytes, offset, (int)layout1.GlobalByteOffsetStart + offset + layout1.HeaderBytes.Length, nationColorPart), "secondPartBlocks");

                Helper.WithMeasurement(() =>
                {
                    imageGenerator.RenderShapeBlocks(imageView, firstPartBlocks, layout1);
                    imageGenerator.RenderCounterBlocksOnBitmap(imageView, firstPartBlocks, secondPartBlocks, layout1, imagePaletteArray, generalPaletteColors);
                }, "RenderCounterBlocksOnBitmap");

                Helper.WithMeasurement(() => renderer.RenderImage(bitMap, imageView), "Render on bitmap");
            }

            return bitMap;
        }
    }
}
