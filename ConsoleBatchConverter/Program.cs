﻿using System;
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
            var gpDirectory = @"C:\GpArch\GP\CorrectRendering";
            var pngDirectory = @"C:\GpArch\png";

            if (!Directory.Exists(pngDirectory))
            {
                Directory.CreateDirectory(pngDirectory);
            }

            var gpFiles = Directory.GetFiles(gpDirectory, "*.gp");

            foreach (var gpFile in gpFiles)
            {
                SaveAsPng(gpFile, pngDirectory);
            }
        }

        private static void SaveAsPng(string gpFile, string pngDirectory)
        {
            var extractResult = new Extractor().ExtractFromGp(gpFile);

            var bitMap = new Bitmap(600, 600);

            IRenderer renderer = new BitmapRenderer(bitMap);
            
            var paletteBytes = File.ReadAllBytes(@"..\..\..\palette\0\agew_1.pal");

            var rawParser = new RawParser();

            var colorCollection = rawParser.GetColorCollectionFromPalleteFile(paletteBytes);

            var imagePaletteColors = ImageGenerator.OffsetsToColors(extractResult.PaletteBytes, colorCollection);
            
            new Runner().Run(extractResult, rawParser, renderer, imagePaletteColors);

            byte[] result = null;
            using (MemoryStream stream = new MemoryStream())
            {
                bitMap.Save(stream, ImageFormat.Png);
                File.WriteAllBytes(Path.Combine(pngDirectory, Path.GetFileNameWithoutExtension(gpFile)+".png"),stream.ToArray());
            }

        }
    }
}
