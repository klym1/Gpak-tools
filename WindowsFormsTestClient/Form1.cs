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
            var logPath = @"..\..\..\extractor_out.txt";
            File.Create(logPath).Dispose();

            IBinaryMapper mapper = new BinaryAutoMapper();

            var extractor = new Extractor(logPath, mapper);

            var extractResult = extractor.ExtractFromGp(@"..\..\..\gp\test15.gp");
           
            var layout = extractResult.LayoutCollection.Last();

            var imageBytes = layout.Bytes.Skip(22).ToArray();
            
            Helper.DumpArray(layout.Bytes,0,64);

            var piactureElements = new Collection<MultiPictureEl>();

            var rowIndex = 0;

            var i = 0;
            while (!(imageBytes[i] == 0xCD && imageBytes[i + 1] == 0xFF))
            {
                int blockType = imageBytes[i];

                if (imageBytes[i] == 0xCD && imageBytes[i+1] == 0xFF)
                {
                    i += 1;
                    continue;
                }
                
                if (imageBytes[i] == 0xFF)
                {
                    var h = true;
                }

                var bytesInBlock = blockType * 2 + 1;

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
                    bytesInBlock = 1;
                }

                if (blockType >= 0 && blockType < 10)
                {
                    Debug.Write(string.Format("{0:d3}. ", rowIndex));
                    Helper.DumpArray(imageBytes, i, bytesInBlock);

                    var emptyCollection = new Collection<Block>();

                    for (int k = 0; k < blockType*2; k+=2)
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

                }

                else
                {
                    Debug.WriteLine("Unknow blocktype");
                }

                i += bytesInBlock;
        
            }

            //Helper.DumpArray(imageBytes, i, 100);
            i++;

//            for (int j = 0; j < imageBytes.Length-17-8-i; j += 17)
//            {
//                Debug.WriteLine("{0:D3}. {1:D2} {2:D2} {3:D2} {4:D2} {5:D2} {6:D2} {7:D2} {8:D2} {9:D2}", z1,
//                           imageBytes[i + j],
//                           imageBytes[i + j + 1],
//                           imageBytes[i + j + 2],
//                           imageBytes[i + j + 3],
//                           imageBytes[i + j + 4],
//                           imageBytes[i + j + 5],
//                           imageBytes[i + j + 6],
//                           imageBytes[i + j + 7],
//                           imageBytes[i + j + 8]);
//                
//            }

            IRenderer renderer = new Renderer();
            pictureBox2.Image = renderer.RenderBitmap(piactureElements);
        }
    }
}
