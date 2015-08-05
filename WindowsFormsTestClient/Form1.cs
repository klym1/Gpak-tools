using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
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

            var extractor = new Extractor(logPath);

            var extractResult = extractor.ExtractFromGp(@"..\..\..\gp\test15.gp");
           
            var layout = extractResult.LayoutCollection.Last();

            Helper.DumpArray(layout.Bytes);

            var imageBytes = layout.Bytes.Skip(30).ToArray();
            int z1 = 0;
            var bytesInBlock = 3;

            var piactureElements = new Collection<MultiPictureEl>();

            var rowIndex = 0;
            
            for (var i = 0; i < imageBytes.Length - 5; i += bytesInBlock)
            {
                int blockType = imageBytes[i];
                
                    switch (blockType)
                    {
                        case 0: bytesInBlock = 1; break;
                        case 1: bytesInBlock = 3; break;
                        //case 0xd0: bytesInBlock = 16; break;
                        case 2: bytesInBlock = 5; break;
                        case 3: bytesInBlock = 7; break;
                        case 4: bytesInBlock = 9; break;
                        case 6: bytesInBlock = 3; break;
                    }
                    
                if (blockType == 1)
                {
                    Debug.WriteLine("{0:D3}. {1:D2} {2:D2} {3:D2}", z1,
                        imageBytes[i],
                        imageBytes[i + 1],
                        imageBytes[i + 2]

                        );

                    piactureElements.Add(new MultiPictureEl(new Collection<Block> { new Block
                    {
                        offsetx = imageBytes[i + 1],
                        length = imageBytes[i + 2],
                    } })
                    {
                        RowIndex = rowIndex++
                    });
                }
                else if (blockType == 0)
                {
                    Debug.WriteLine("{0:D3}. {1:D2} {2:D2} {3:D2}", z1,
                       imageBytes[i],
                       imageBytes[i + 1],
                       imageBytes[i + 2]

                       );

                    new MultiPictureEl(new Collection<Block> {new Block{
                        offsetx = imageBytes[i + 1],
                        length = imageBytes[i + 2],
                    }})
                    {
                        RowIndex = rowIndex++
                    };


                }
                
                else if (blockType == 3)
                {
                    Debug.WriteLine("{0:D3}. {1:D2} {2:D2} {3:D2} {4:D2} {5:D2} {6:D2} {7:D2}", z1,
                        imageBytes[i],
                        imageBytes[i + 1],
                        imageBytes[i + 2],
                        imageBytes[i + 3],
                        imageBytes[i + 4],
                        imageBytes[i + 5],
                        imageBytes[i + 6]);

                       
                   piactureElements.Add(new MultiPictureEl(new Collection<Block> { new Block
                    {
                        offsetx = imageBytes[i + 1],
                        length = imageBytes[i + 2],
                    }, new Block
                    {
                        offsetx = imageBytes[i + 3],
                        length = imageBytes[i + 4],
                    }, new Block
                    {
                        offsetx = imageBytes[i + 5],
                        length = imageBytes[i + 6],
                    }  })
                    {
                        RowIndex = rowIndex++
                    });

                  
                }

                else if (blockType == 4)
                {
                    Debug.WriteLine("{0:D3}. {1:D2} {2:D2} {3:D2} {4:D2} {5:D2} {6:D2} {7:D2} {8:D2} {9:D2}", z1,
                        imageBytes[i],
                        imageBytes[i + 1],
                        imageBytes[i + 2],
                        imageBytes[i + 3],
                        imageBytes[i + 4],
                        imageBytes[i + 5],
                        imageBytes[i + 6],
                        imageBytes[i + 7],
                        imageBytes[i + 8]);

                   piactureElements.Add(new MultiPictureEl(new Collection<Block> { new Block
                    {
                        offsetx = imageBytes[i + 1],
                        length = imageBytes[i + 2],
                    }, new Block
                    {
                        offsetx = imageBytes[i + 3],
                        length = imageBytes[i + 4],
                    }, new Block
                    {
                        offsetx = imageBytes[i + 5],
                        length = imageBytes[i + 6],
                    } ,
                   new Block
                    {
                        offsetx = imageBytes[i + 7],
                        length = imageBytes[i + 8],
                    }})
                    {
                        RowIndex = rowIndex++
                    });

                  
                }

                else if (blockType == 2)
                {
                    Debug.WriteLine("{0:D3}. {1:D2} {2:D2} {3:D2} {4:D2}", z1,
                        imageBytes[i],
                        imageBytes[i + 1],
                        imageBytes[i + 2],
                        imageBytes[i + 3]
                        );

                    piactureElements.Add(new MultiPictureEl(new Collection<Block>
                    {
                        new Block
                        {
                            offsetx = imageBytes[i + 1],
                            length = imageBytes[i + 2],
                        },
                        new Block
                        {
                            offsetx = imageBytes[i + 3],
                            length = 1
                        }
                    })
                    {
                        RowIndex = rowIndex++
                    });


                }
                else if (blockType == 6)
                {
                    Debug.WriteLine("{0:D3}. {1:D2} {2:D2} {3:D2}", z1,
                  imageBytes[i],
                  imageBytes[i + 1],
                  imageBytes[i + 2]

                  );
                }
                else if (blockType == 0xd0)
                {
                    Debug.WriteLine("{0:D3}. {1:D2} {2:D2} {3:D2} {4:D2} {5:D2} {6:D2} {7:D2}", z1,
                        imageBytes[i],
                        imageBytes[i + 1],
                        imageBytes[i + 2],
                        imageBytes[i + 3],
                        imageBytes[i + 4],
                        imageBytes[i + 5],
                        imageBytes[i + 6]);
                }
                else
                {
                    Debug.WriteLine("Unknow blocktype");

                    Debug.WriteLine("{0:D3}. {1:D2} {2:D2} {3:D2} {4:D2} {5:D2} {6:D2} {7:D2} {8:D2} {9:D2}", z1,
                        imageBytes[i],
                        imageBytes[i + 1],
                        imageBytes[i + 2],
                        imageBytes[i + 3],
                        imageBytes[i + 4],
                        imageBytes[i + 5],
                        imageBytes[i + 6],
                        imageBytes[i + 7],
                        imageBytes[i + 8]);
                }

                z1++;
            }

            IRenderer renderer = new Renderer();
            pictureBox2.Image = renderer.RenderBitmap(piactureElements);
        }
    }
}
