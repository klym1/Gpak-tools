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

            var bitMap = new Bitmap(600, 600);

            IRenderer renderer = new BitmapRenderer(bitMap);
            
            pictureBox2.Image = bitMap;

            var paletteBytes = File.ReadAllBytes(@"..\..\..\palette\0\agew_1.pal");

            var rawParser = new RawParser();

            var colorCollection = rawParser.GetColorCollectionFromPalleteFile(paletteBytes);

            var imagePaletteColors = OffsetsToColors(extractResult.PaletteBytes, colorCollection);

            Helper.WithMeasurement(
                () => Run(extractResult, rawParser, renderer, imagePaletteColors), 
                name : "Run", 
                onFinish: elapsed => label1.Text = String.Format("{0:D}", elapsed.Milliseconds));
        }

        private void Run(ExtractorResult extractResult, RawParser rawParser, IRenderer renderer, List<Color> imagePaletteColors)
        {
            var imageView = new ImageView(600, 600);

            Helper.WithMeasurement(renderer.SetupCanvas, name: "SetupCanvas");

            var imageGenerator = new ImageGenerator();

            foreach (var layout in extractResult.LayoutCollection.Take(1))
            {
                int offset = 0;

                ImageLayoutInfo layout1 = layout;
                
                var firstPartBlocks = Helper.WithMeasurement(() =>
                {
                    var rawFirstPartBlocks = rawParser.ParseRawBlockGroups(layout1.Bytes, out offset);
                    return rawFirstPartBlocks.ConvertToAbsoluteCoordinatesBlocks();

                }, "firstPartBlocks");
                
                var secondPartBlocks = Helper.WithMeasurement(() => rawParser.GetRawColorBlocks(layout1.Bytes, offset), "secondPartBlocks");

                Helper.WithMeasurement(() => imageGenerator.RenderCounterBlocksOnBitmap(imageView, firstPartBlocks, secondPartBlocks, layout1, imagePaletteColors), "RenderCounterBlocksOnBitmap");
                
                renderer.RenderImage(imageView);
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
