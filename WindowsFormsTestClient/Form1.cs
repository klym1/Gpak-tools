using System;
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

            var extractResult = extractor.ExtractFromGp(@"..\..\..\gp\test15_.gp");

            IRenderer renderer = new Renderer();

            var bitMap = new Bitmap(600, 600);
            SetupCanvas(bitMap);

            var i = 0;
            foreach (var layout in extractResult.LayoutCollection.Take(5))
            {
                var tuple = MultiPictureEls(layout.Bytes);
                Debug.WriteLine("Offset: " + tuple.Item2);

                renderer.RenderBitmap(bitMap, tuple.Item1, layout);

                i++;
            }


            pictureBox2.Image = bitMap;
        }

        private static void SetupCanvas(Bitmap bitMap)
        {
            using (var graphics = Graphics.FromImage(bitMap))
            {
                graphics.FillRectangle(new SolidBrush(Color.SpringGreen),
                    new Rectangle(0, 0, bitMap.Width, bitMap.Height));
            }
        }

        private static Tuple<Collection<MultiPictureEl>,int> MultiPictureEls(byte[] imageBytes)
        {
            var piactureElements = new Collection<MultiPictureEl>();

            var rowIndex = 0;

            var i = 0;
            while (!(imageBytes[i] == 0xCD && imageBytes[i + 1] == 0xFF))
            {
                int blockType = imageBytes[i];

                if (blockType == 0xE1) // magic number. 1-byte coded block. Investigate other similar cases
                {
                    //E1  4B  E1  C7  => 1 27 20 1 23 28

                    var nextByte = imageBytes[i + 1];
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

                    i += 2;
                    continue;
                }

                if (blockType > 20)
                {
                    Debug.WriteLine("Unknow blocktype");
                    throw new Exception("wrong block type");
                }

                //ordinary processing
                var bytesInBlock = blockType*2 + 1;

                Debug.Write(string.Format("{0:d3}. ", rowIndex));
                Helper.DumpArray(imageBytes, i, bytesInBlock);

                var emptyCollection = new Collection<Block>();

                for (int k = 0; k < blockType*2; k += 2)
                {
                    emptyCollection.Add(new Block
                    {
                        offsetx = imageBytes[i + k + 1],
                        length = imageBytes[i + k + 2],
                    });
                }

                piactureElements.Add(new MultiPictureEl(emptyCollection)
                {
                    RowIndex = rowIndex++
                });

                i += bytesInBlock;
            }

            return Tuple.Create(piactureElements, i);
        }
    }
}
