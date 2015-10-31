using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPExtractor;
using ImageRenderer;

namespace ConsoleBatchConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            const string gpDirectory = @"C:\GpArch\GP";
            const string pngDirectory = @"C:\GpArch\png";

            if (!Directory.Exists(pngDirectory))
            {
                Directory.CreateDirectory(pngDirectory);
            }

            var gpFiles = Directory.GetFiles(gpDirectory, "*.gp");

            foreach (var gpFile in gpFiles)
            {
                try
                {
                    SaveAsPng(gpFile, pngDirectory);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error in: " + gpFile);
                }
            }

            Console.ReadKey();
        }

        private static void SaveAsPng(string gpFile, string pngDirectory)
        {
            IRenderer renderer = new BitmapRenderer();

            var imagePaletteBytes = new Extractor().GetPaletteBytes(gpFile);
            var extractResult = new Extractor().GetImagesFromOutput(gpFile).ToList();

            var rawParser = new RawParser();

            var paletteBytes = File.ReadAllBytes(@"..\..\..\palette\0\agew_1.pal");
            var colorCollection = rawParser.GetColorCollectionFromPalleteFile(paletteBytes);

            var imagePaletteColors = ImageGenerator.OffsetsToColors(imagePaletteBytes, colorCollection);

            var nationColorOffset = NationColorOffset.Red;

            using (var bitMap = new Runner().Run(extractResult, nationColorOffset, 0, rawParser, renderer, imagePaletteColors, colorCollection))
            {
                using (var stream = new MemoryStream())
                {
                    bitMap.Save(stream, ImageFormat.Png);
                    File.WriteAllBytes(Path.Combine(pngDirectory, Path.GetFileNameWithoutExtension(gpFile) + ".png"),
                        stream.ToArray());
                }
            }
        }
    }
}
