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
using ImageLayout = GPExtractor.ImageLayout;

namespace WindowsFormsTestClient
{
    public partial class Form1 : Form
    {
        private void Do()
        {
            var extractResult = new Extractor().GetImagesFromOutput(@"c:\GpArch\gp\hh.gp");
            var imagePaletteBytes = new Extractor().GetPaletteBytes(@"c:\GpArch\gp\hh.gp");

            IRenderer renderer = new BitmapRenderer();
            
            var paletteBytes = File.ReadAllBytes(@"..\..\..\palette\0\agew_1.pal");

            var rawParser = new RawParser();

            var colorCollection = rawParser.GetColorCollectionFromPalleteFile(paletteBytes);

            var imagePaletteColors = ImageGenerator.OffsetsToColors(imagePaletteBytes, colorCollection);

            RenderPalette(imagePaletteColors);
            RenderGeneralPalette(colorCollection.ToList());

            var itemsForListBox = Enumerable.Range(0, extractResult.Count).Select(it => it.ToString()).ToList();

            listBox1.DataSource = itemsForListBox;

            DrawImage(extractResult, 0, rawParser, renderer, imagePaletteColors, colorCollection);

            listBox1.SelectedIndexChanged += (sender, args) =>
            {
                DrawImage(extractResult, (sender as ListBox).SelectedIndex, rawParser, renderer, imagePaletteColors, colorCollection);
            };
        }

        private void DrawImage(IList<ImageLayout> extractResult, int index, RawParser rawParser, IRenderer renderer, List<Color> imagePaletteColors,
            Collection<Color> colorCollection)
        {
            pictureBox2.Image = Helper.WithMeasurement(
                () => new Runner().Run(extractResult, index, rawParser, renderer, imagePaletteColors, colorCollection.ToList()),
                name: "Run",
                onFinish: elapsed => label1.Text = String.Format("{0:D}", elapsed.Milliseconds));
        }

        private void RenderGeneralPalette(List<Color> imagePaletteColors)
        {
            var paletteBitMap = new Bitmap(500, 500);
            IRenderer paletteRenderer = new BitmapRenderer();
            paletteRenderer.RenderPalette(paletteBitMap, imagePaletteColors, 200, 8);
            pictureBox1.Image = paletteBitMap;
        }

        private void RenderPalette(List<Color> imagePaletteColors)
        {
            var paletteBitMap = new Bitmap(500, 500);
            IRenderer paletteRenderer = new BitmapRenderer();
            paletteRenderer.RenderPalette(paletteBitMap, imagePaletteColors, 200, 4);
            pictureBox3.Image = paletteBitMap;
        }
        
        public Form1()
        {
            InitializeComponent();

            Load += (sender, args) => Do();
        }
    }
}
