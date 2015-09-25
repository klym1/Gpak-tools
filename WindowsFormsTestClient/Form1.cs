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

            var extractResult = extractor.ExtractFromGp(@"c:\GpArch\gp\test19.gp");

            IRenderer renderer = new Renderer();

            var bitMap = new Bitmap(600, 600);
            SetupCanvas(bitMap);

            pictureBox2.Image = bitMap;

           // var paletteBytes = File.ReadAllBytes(@"..\..\..\palette\0\old.pal");
            var paletteBytes = File.ReadAllBytes(@"..\..\..\palette\0\agew_1.pal");

            GetColorCollectionFromPalleteFile(paletteBytes);

            pictureBox3.Image = renderer.RenderPalette(colorCollection, 100, pixelSize:10);


            var imagePaletteColors = OffsetsToColors(extractResult.PaletteBytes);

            var paletteImage = renderer.RenderPalette(imagePaletteColors, 139, pixelSize: 1);

            pictureBox4.Image = paletteImage;
            
            var i = 0;
            foreach (var layout in extractResult.LayoutCollection.Take(5))
            {
                var tupleCollection = MultiPictureEls(layout.Bytes);

                var firstPartBlocks = tupleCollection.Item1;

                var secondPartBlocks = SecondPart(layout.Bytes, tupleCollection.Item2);

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

        private static Collection<CounterSection> SecondPart(byte[] imageBytes, int initialOffset)
        {
            var offset = initialOffset+1; // skip CD bytes

            var tempByteCollection = new Collection<CounterBlock>();

            var collectionOfBlocks = new Collection<CounterSection>();

            var row = 0;

            while (offset < imageBytes.Length-1)
            {
                var blockStartByte = imageBytes[offset];

                var blockLength = blockStartByte & 0x0f;
                var blockType = (blockStartByte >> 4);

                if (blockType != 0xf)
                {
                    //
                    var h = 5;
                    break;
                }

                if (blockLength < 15) // last block
                {
                    var p = imageBytes.Length - offset - 1;

                    blockLength = p > 15 ? 15: p;
                }

                offset++;

                for (var i = 0; i < blockLength; i+=2)
                {
                    var block = new CounterBlock(imageBytes[offset], imageBytes[offset + 1]);
                    tempByteCollection.Add(block);
                    offset+=2;
                }

                collectionOfBlocks.Add(new CounterSection(tempByteCollection.ToList())
                {
                    Row = row++
                });
                tempByteCollection = new Collection<CounterBlock>();
            }
            
            return collectionOfBlocks;
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

            return Tuple.Create(piactureElements.MergeBlocks(), offset);
        }

        private Collection<Color> colorCollection = new Collection<Color>();

        private List<Color> OffsetsToColors(byte[] imagePaletteOffsets)
        {
            return imagePaletteOffsets.Select(offset => colorCollection[offset]).ToList();
        }
    }
}
