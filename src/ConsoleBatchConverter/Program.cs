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
                Console.Error.WriteLine("  Writes every frame of every .gp file as <name>_<frame>.png.");
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

            var renderer = new GpRenderer(Palette.Load(paletteFile));

            // Match *.gp case-insensitively: Linux file systems are case-sensitive
            // and the original game ships both *.gp and *.GP files.
            var gpFiles = Directory.EnumerateFiles(gpDirectory)
                .Where(file => string.Equals(Path.GetExtension(file), ".gp", StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToList();

            var failed = 0;
            var frames = 0;
            var skippedParts = 0;

            foreach (var gpFile in gpFiles)
            {
                try
                {
                    var file = GpFile.Load(gpFile);
                    var name = Path.GetFileNameWithoutExtension(gpFile);

                    foreach (var frame in file.Frames)
                    {
                        var rendered = renderer.Render(file, frame);
                        PngWriter.Save(rendered.Image, Path.Combine(pngDirectory, name + "_" + frame.Index.ToString("D3") + ".png"));

                        frames++;
                        skippedParts += rendered.SkippedParts.Count;
                    }
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is IOException || ex is NotSupportedException)
                {
                    failed++;
                    Console.Error.WriteLine("Error in: " + gpFile + ": " + ex.Message);
                }
            }

            Console.WriteLine("Converted {0} of {1} files ({2} frames) to {3}", gpFiles.Count - failed, gpFiles.Count, frames, pngDirectory);

            if (skippedParts > 0)
            {
                Console.WriteLine("Skipped {0} frame parts with unsupported encodings", skippedParts);
            }

            return failed == 0 ? 0 : 2;
        }
    }
}
