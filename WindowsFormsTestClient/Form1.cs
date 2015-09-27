using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
            const string logPath = @"..\..\..\extractor_out.txt";

            IBinaryMapper mapper = new BinaryAutoMapper();

            var extractor = new Extractor(logPath, mapper);

            var extractResult = extractor.ExtractFromGp(@"c:\GpArch\gp\test18.gp");

            IRenderer renderer = new Renderer();

            var bitMap = new Bitmap(600, 600);
            SetupCanvas(bitMap);

            pictureBox2.Image = bitMap;

            var paletteBytes = File.ReadAllBytes(@"..\..\..\palette\0\agew_1.pal");

            GetColorCollectionFromPalleteFile(paletteBytes);

            pictureBox3.Image = renderer.RenderPalette(colorCollection, 100, pixelSize:10);


            var imagePaletteColors = OffsetsToColors(extractResult.PaletteBytes);

            var paletteImage = renderer.RenderPalette(imagePaletteColors, 139, pixelSize: 1);

            pictureBox4.Image = paletteImage;

            var rawParser = new RawParser();

            var i = 0;
            foreach (var layout in extractResult.LayoutCollection.Take(1))
            {
                int offset;

                var rawFirstPartBlocks = rawParser.ParseRawBlockGroups(layout.Bytes, out offset);
                var firstPartBlocks = GetAbsoluteBlocks(rawFirstPartBlocks);

                var secondPartBlocks = rawParser.SecondPart(layout.Bytes, offset);

                renderer.RenderBitmap(bitMap, firstPartBlocks, secondPartBlocks, layout, imagePaletteColors);

                renderer.RenderCounterBlocksOnBitmap(bitMap, firstPartBlocks, secondPartBlocks, layout, imagePaletteColors);

                i++;
            }

            
        }

        private void GetColorCollectionFromPalleteFile(byte[] paletteBytes)
        {
            for (int i = 0; i < paletteBytes.Length - 2; i += 3)
            {
                colorCollection.Add(Color.FromArgb(255, paletteBytes[i], paletteBytes[i + 1], paletteBytes[i + 2]));
            }
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
            var absoluteBlocks = blockGroups.ConvertToAbsoluteCoordinatesBlocks();
            return absoluteBlocks;
        }
        
        private Collection<Color> colorCollection = new Collection<Color>();

        private List<Color> OffsetsToColors(byte[] imagePaletteOffsets)
        {
            return imagePaletteOffsets.Select(offset => colorCollection[offset]).ToList();
        }
    }
}
