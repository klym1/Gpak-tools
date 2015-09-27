﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GPExtractor;
using ImageRenderer;
using Types;

namespace WindowsFormsTestClient
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            Load += (sender, args) => Do();
        }

        private void Do()
        {
            var extractResult = new Extractor().ExtractFromGp(@"c:\GpArch\gp\test20.gp");

            IRenderer renderer = new Renderer();

            var bitMap = new Bitmap(600, 600);
            SetupCanvas(bitMap);

            pictureBox2.Image = bitMap;

            var paletteBytes = File.ReadAllBytes(@"..\..\..\palette\0\agew_1.pal");

            var colorCollection = GetColorCollectionFromPalleteFile(paletteBytes);

           // pictureBox3.Image = renderer.RenderPalette(colorCollection, 100, pixelSize:10);
            var imagePaletteColors = OffsetsToColors(extractResult.PaletteBytes, colorCollection);

            //var paletteImage = renderer.RenderPalette(imagePaletteColors, 139, pixelSize: 1);

            //pictureBox4.Image = paletteImage;

            var rawParser = new RawParser();

            var sw = Stopwatch.StartNew();

            Run(extractResult, rawParser, renderer, bitMap, imagePaletteColors);

            sw.Stop();

            label1.Text = sw.ElapsedMilliseconds.ToString("D");
        }

        private void Run(ExtractorResult extractResult, RawParser rawParser, IRenderer renderer, Bitmap bitMap,
            List<Color> imagePaletteColors)
        {
            foreach (var layout in extractResult.LayoutCollection.Take(1))
            {
                int offset;

                var rawFirstPartBlocks = rawParser.ParseRawBlockGroups(layout.Bytes, out offset);
                var firstPartBlocks = GetAbsoluteBlocks(rawFirstPartBlocks);

                var secondPartBlocks = rawParser.GetRawColorBlocks(layout.Bytes, offset);

                //renderer.RenderBitmap(bitMap, firstPartBlocks, layout);

                renderer.RenderCounterBlocksOnBitmap(bitMap, firstPartBlocks, secondPartBlocks, layout, imagePaletteColors);
            }
        }

        private Collection<Color> GetColorCollectionFromPalleteFile(byte[] paletteBytes)
        {
            var colorCollection = new Collection<Color>();

            for (int i = 0; i < paletteBytes.Length - 2; i += 3)
            {
                colorCollection.Add(Color.FromArgb(255, paletteBytes[i], paletteBytes[i + 1], paletteBytes[i + 2]));
            }

            return colorCollection;
        }

        private static void SetupCanvas(Bitmap bitMap)
        {
            using (var graphics = Graphics.FromImage(bitMap))
            {
                graphics.FillRectangle(new SolidBrush(Color.White),
                    new Rectangle(0, 0, bitMap.Width, bitMap.Height));
            }
        }

        private List<AbsoluteBlock> GetAbsoluteBlocks(RawShapeBlocksGroup[] blockGroups)
        {
            return blockGroups.ConvertToAbsoluteCoordinatesBlocks();
        }
        
        private List<Color> OffsetsToColors(byte[] imagePaletteOffsets, Collection<Color> colorCollection)
        {
            return imagePaletteOffsets.Select(offset => colorCollection[offset]).ToList();
        }
    }
}
