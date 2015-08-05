using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
            const string logPath = @"..\..\..\extractor_out.txt";
            File.Create(logPath).Dispose();

            IBinaryMapper mapper = new BinaryAutoMapper();

            var extractor = new Extractor(logPath, mapper);

            var extractResult = extractor.ExtractFromGp(@"..\..\..\gp\test15.gp");
           
            var layout = extractResult.LayoutCollection.Last();

            var imageBytes = layout.Bytes;
            
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

                    piactureElements.Add(new MultiPictureEl(new Collection<Block>{new Block
                    {
                        length = a,
                        offsetx = b
                    }})
                    {
                        RowIndex = rowIndex++
                    });

                    i += 2;
                    continue;
                }

                if (blockType > 10)
                {
                    Debug.WriteLine("Unknow blocktype");
                    throw new Exception("wrong block type");
                }

                //ordinary processing
                var bytesInBlock = blockType * 2 + 1;
                
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

            Debug.WriteLine("Offset: "  + i);

            IRenderer renderer = new Renderer();
            pictureBox2.Image = renderer.RenderBitmap(piactureElements);
        }
    }
}
