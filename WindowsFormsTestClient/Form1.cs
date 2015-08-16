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
            File.Create(logPath).Dispose();

            IBinaryMapper mapper = new BinaryAutoMapper();

            var extractor = new Extractor(logPath, mapper);

            var extractResult = extractor.ExtractFromGp(@"..\..\..\gp\test15.gp");

            IRenderer renderer = new Renderer();

            var bitMap = new Bitmap(600, 600);
            SetupCanvas(bitMap);

            pictureBox2.Image = bitMap;

            var paletteBytes = File.ReadAllBytes(@"..\..\..\palette\0\old.pal");

            GetColorCollectionFromPalleteFile(paletteBytes);

            pictureBox3.Image = renderer.RenderPalette(colorCollection, 100, pixelSize:10);


            var imagePaletteColors = OffsetsToColors(extractResult.PaletteBytes);

            var paletteImage = renderer.RenderPalette(imagePaletteColors, 111, pixelSize: 1);

            renderer.DrawHorizontalColorLine(paletteImage, imagePaletteColors.Skip(30).Take(30).ToList(), 50, 50);

            pictureBox4.Image = paletteImage;

            var i = 0;
            foreach (var layout in extractResult.LayoutCollection.Take(5))
            {
                var tupleCollection = MultiPictureEls(layout.Bytes);

                var countOffset = tupleCollection.Item1.Select(it => it.Collection.Count*2 + 1).Sum();

                var partsCount = tupleCollection.Item1.Select(it => it.Collection.Count).Sum();

                var twentyThirdRow =
                    tupleCollection.Item1.Where(it => it.RowIndex < 32).Select(it => it.Collection.Count).Sum();

                twentyThirdRow += tupleCollection.Item1.First(it => it.RowIndex == 32).Collection.Count;

                var allpartsLength = tupleCollection.Item1.Select(it => it.Collection.Sum(o => o.length)).Sum();

                var result = SecondPart(layout.Bytes, countOffset, partsCount);
                
                //Debug.WriteLine("Offset: " + countOffset);

                renderer.RenderBitmap(bitMap, tupleCollection.Item1, layout);

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

        private static int SecondPart(byte[] imageBytes, int initialOffset, int partsCount)
        {
            for (int i = 0; i < (imageBytes.Length - initialOffset)/ 17; i ++)
            {
            //    Debug.Write(i + " - ");
            //    Helper.DumpArray(imageBytes, initialOffset + i*17 + 1, 17);
            }

            var tupleCollection = new Collection<Tuple<byte, byte, int>> ();

            var pairsProcessed = 0;
            var offset = initialOffset + 2; // skip CD FF bytes
            var sumOfThirdOctets = 0;

            var pairNumber = 0;

            while (offset < imageBytes.Length - 10)
            {
                //Debug.Write(pairNumber + " - ");
                //Helper.DumpArray(imageBytes, offset, 2);

                var byte1 = imageBytes[offset];
                var byte2 = imageBytes[offset + 1];

                var newnumber = (byte1) | ((byte2 & 0x0f) << 8);
                
                var thirdOctet = (byte2 >> 4);

                if (thirdOctet == 0xd)
                {
                    tupleCollection.Add(Tuple.Create(byte1, byte2, newnumber));
                }

                sumOfThirdOctets += thirdOctet;

                //tupleCollection.Add(Tuple.Create(byte1, byte2, newnumber));

                offset += 2;
                pairNumber++;

                pairsProcessed++;

                if (pairsProcessed == 8)
                {
                    pairsProcessed = 0;
                    offset++;
                    
                }
            }
            return 0;
        }

        private static Tuple<Collection<MultiPictureEl>,int> MultiPictureEls(byte[] imageBytes)
        {
            var piactureElements = new Collection<MultiPictureEl>();

            var rowIndex = 0;

            var offset = 0;
            while (!(imageBytes[offset] == 0xCD))
            {
                int blockType = imageBytes[offset];

                if (blockType == 0xE1) // magic number. 1-byte coded block. Investigate other similar cases
                {
                    //E1  4B  E1  C7  => 1 27 20 1 23 28

                    var nextByte = imageBytes[offset + 1];
                    var a = (nextByte >> 4) | (1 << 4);
                    var b = (nextByte & 0xf) | (1 << 4);

                    piactureElements.Add(new MultiPictureEl(new Collection<Block>
                    {
                        new Block
                        {
                            length = a,
                            offsetx = b
                        }
                    })
                    {
                        RowIndex = rowIndex++
                    });

                    offset += 2;
                    continue;
                }

                if (blockType > 20)
                {
                    Debug.WriteLine("Unknow blocktype");
                    throw new Exception("wrong block type");
                }

                //ordinary processing
                var bytesInBlock = blockType*2 + 1;

                //Debug.Write(string.Format("{0:d3}. ", rowIndex));
                //Helper.DumpArray(imageBytes, offset, bytesInBlock);

                var emptyCollection = new Collection<Block>();

                for (int k = 0; k < blockType*2; k += 2)
                {
                    emptyCollection.Add(new Block
                    {
                        offsetx = imageBytes[offset + k + 1],
                        length = imageBytes[offset + k + 2],
                    });
                }

                piactureElements.Add(new MultiPictureEl(emptyCollection)
                {
                    RowIndex = rowIndex++
                });

                offset += bytesInBlock;
            }

            return Tuple.Create(piactureElements, offset);
        }

        private Collection<Color> colorCollection = new Collection<Color>();

        private List<Color> OffsetsToColors(byte[] imagePaletteOffsets)
        {
            return imagePaletteOffsets.Select(offset => colorCollection[offset]).ToList();
        }
    }
}
