using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows.Forms;
using GPExtractor;
using ImageRenderer;
using Types;

namespace WindowsFormsTestClient
{
    public partial class Form1 : Form
    {
        private void Do()
        {
            var extractResult = new Extractor().ExtractFromGp(@"c:\GpArch\gp\test18.gp");

            IRenderer renderer = new Renderer();

            var bitMap = new Bitmap(600, 600);
            
            pictureBox2.Image = bitMap;

            var paletteBytes = File.ReadAllBytes(@"..\..\..\palette\0\agew_1.pal");

            var rawParser = new RawParser();

            var colorCollection = rawParser.GetColorCollectionFromPalleteFile(paletteBytes);

            var imagePaletteColors = OffsetsToColors(extractResult.PaletteBytes, colorCollection);

            Helper.WithMeasurement(() => Run(extractResult, rawParser, renderer, bitMap, imagePaletteColors), onFinish: elapsed => label1.Text = String.Format("{0:D}", elapsed.Milliseconds));
        }

        private void Run(ExtractorResult extractResult, RawParser rawParser, IRenderer renderer, Bitmap bitMap,
            List<Color> imagePaletteColors)
        {
            renderer.SetupCanvas(bitMap);

            foreach (var layout in extractResult.LayoutCollection.Take(1))
            {
                int offset = 0;

                ImageLayoutInfo layout1 = layout;

                var rawFirstPartBlocks = Helper.WithMeasurement(() => rawParser.ParseRawBlockGroups(layout1.Bytes, out offset), "rawFirstPartBlocks");
                var firstPartBlocks = Helper.WithMeasurement(() => rawFirstPartBlocks.ConvertToAbsoluteCoordinatesBlocks(), "firstPartBlocks");
                var secondPartBlocks = Helper.WithMeasurement(() => rawParser.GetRawColorBlocks(layout1.Bytes, offset), "secondPartBlocks");
                
                Helper.WithMeasurement(() => renderer.RenderCounterBlocksOnBitmap(bitMap, firstPartBlocks, secondPartBlocks, layout1, imagePaletteColors), "RenderCounterBlocksOnBitmap");
            }
        }
        
        private List<Color> OffsetsToColors(byte[] imagePaletteOffsets, Collection<Color> colorCollection)
        {
            return imagePaletteOffsets.Select(offset => colorCollection[offset]).ToList();
        }

        public Form1()
        {
            InitializeComponent();

            Load += (sender, args) => Do();
        }
    }
}
