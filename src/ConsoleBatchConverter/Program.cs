using System;
using System.IO;
using System.Linq;
using GPExtractor;
using ImageRenderer;

namespace ConsoleBatchConverter
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ConsoleBatchConverter <gp-directory> <png-output-directory> [palette-file]");
                Console.Error.WriteLine("  palette-file defaults to palette/0/agew_1.pal next to the executable.");
                return 1;
            }

            var gpDirectory = args[0];
            var pngDirectory = args[1];
            var paletteFile = args.Length > 2
                ? args[2]
                : Path.Combine(AppContext.BaseDirectory, "palette", "0", "agew_1.pal");

            if (!Directory.Exists(gpDirectory))
            {
                Console.Error.WriteLine("GP directory not found: " + gpDirectory);
                return 1;
            }

            Directory.CreateDirectory(pngDirectory);

            var paletteBytes = File.ReadAllBytes(paletteFile);

            // Match *.gp case-insensitively: Linux file systems are case-sensitive
            // and the original game ships both *.gp and *.GP files.
            var gpFiles = Directory.EnumerateFiles(gpDirectory)
                .Where(file => string.Equals(Path.GetExtension(file), ".gp", StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToList();

            var failed = 0;

            foreach (var gpFile in gpFiles)
            {
                try
                {
                    SaveAsPng(gpFile, pngDirectory, paletteBytes);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.Error.WriteLine("Error in: " + gpFile + ": " + ex.Message);
                }
            }

            Console.WriteLine("Converted {0} of {1} files to {2}", gpFiles.Count - failed, gpFiles.Count, pngDirectory);

            return failed == 0 ? 0 : 2;
        }

        private static void SaveAsPng(string gpFile, string pngDirectory, byte[] paletteBytes)
        {
            IRenderer renderer = new BitmapRenderer();

            var imagePaletteBytes = new Extractor().GetPaletteBytes(gpFile);
            var extractResult = new Extractor().GetImagesFromOutput(gpFile).ToList();

            var rawParser = new RawParser();

            var colorCollection = rawParser.GetColorCollectionFromPalleteFile(paletteBytes);

            var imagePaletteColors = ImageGenerator.OffsetsToColors(imagePaletteBytes, colorCollection);

            var nationColorOffset = NationColorOffset.Red;

            var image = new Runner().Run(extractResult, nationColorOffset, 0, rawParser, renderer, imagePaletteColors, colorCollection);

            PngWriter.Save(image, Path.Combine(pngDirectory, Path.GetFileNameWithoutExtension(gpFile) + ".png"));
        }
    }
}
