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
            var extractResult = new Extractor().ExtractFromGp(@"c:\GpArch\gp\test7_7.gp");

            var bitMap = new Bitmap(600, 600);

            IRenderer renderer = new BitmapRenderer(bitMap);
            
            pictureBox2.Image = bitMap;

            var paletteBytes = File.ReadAllBytes(@"..\..\..\palette\0\agew_1.pal");

            var rawParser = new RawParser();

            var colorCollection = rawParser.GetColorCollectionFromPalleteFile(paletteBytes);

            var imagePaletteColors = ImageGenerator.OffsetsToColors(extractResult.PaletteBytes, colorCollection);

            RenderPalette(imagePaletteColors);
            RenderGeneralPalette(colorCollection.ToList());

            Helper.WithMeasurement(
                () => new Runner().Run(extractResult, rawParser, renderer, imagePaletteColors, colorCollection.ToList()), 
                name : "Run", 
                onFinish: elapsed => label1.Text = String.Format("{0:D}", elapsed.Milliseconds));
        }

        private void RenderGeneralPalette(List<Color> imagePaletteColors)
        {
            var paletteBitMap = new Bitmap(500, 500);
            IRenderer paletteRenderer = new BitmapRenderer(paletteBitMap);
            paletteRenderer.RenderPalette(imagePaletteColors, 200, 4);
            pictureBox1.Image = paletteBitMap;
        }

        private void RenderPalette(List<Color> imagePaletteColors)
        {
            var paletteBitMap = new Bitmap(500, 500);
            IRenderer paletteRenderer = new BitmapRenderer(paletteBitMap);
            paletteRenderer.RenderPalette(imagePaletteColors, 310, 1);
            pictureBox3.Image = paletteBitMap;
        }
        
        public Form1()
        {
            InitializeComponent();

            Load += (sender, args) => Do();
        }
    }
}
