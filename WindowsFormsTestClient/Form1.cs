﻿using System;
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
            var filePath = @"c:\GpArch\gp\hh.gp";

            var imagePaletteBytes = new Extractor().GetPaletteBytes(filePath);
            var extractResult = new Extractor().GetImagesFromOutput(filePath).ToList();
            

            IRenderer renderer = new BitmapRenderer();
            
            var paletteBytes = File.ReadAllBytes(@"..\..\..\palette\0\agew_1.pal");

            var rawParser = new RawParser();

            var colorCollection = rawParser.GetColorCollectionFromPalleteFile(paletteBytes);

            var imagePaletteColors = ImageGenerator.OffsetsToColors(imagePaletteBytes, colorCollection);

            //RenderPalette(imagePaletteColors);
            //RenderGeneralPalette(colorCollection.ToList());

            var bitmapList = DrawImage(extractResult, extractResult.Count, rawParser, renderer, imagePaletteColors, colorCollection);

            var itemsForListBox = Enumerable.Range(0, bitmapList.Count).Select(it => it.ToString()).ToList();

            listBox1.DataSource = itemsForListBox;

            listBox1.SelectedIndexChanged += (sender, args) =>
            {
                if ((sender as ListBox).SelectedIndex > -1)
                pictureBox2.Image = bitmapList[(sender as ListBox).SelectedIndex];
            };

            listBox1.ClearSelected();
            listBox1.SelectedIndex = 0;
        }

        private IList<Bitmap> DrawImage(IList<ImageLayout> extractResult, int numberOfImages, RawParser rawParser, IRenderer renderer, List<Color> imagePaletteColors,
            Collection<Color> colorCollection)
        {
            return Helper.WithMeasurement(
                () =>
                {
                    var bitMapCollection = new Collection<Bitmap>();

                    for (int i = 0; i < numberOfImages; i++)
                    {
                        try
                        {
                            var bitMap = new Runner().Run(extractResult, i, rawParser, renderer, imagePaletteColors, colorCollection.ToList());

                            bitMapCollection.Add(bitMap);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                        }
                    }

                    return bitMapCollection;

                },
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
