using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using GPExtractor;

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

            var extractor = new Extractor(logPath);

            File.Create(logPath).Dispose();

            var extractResult = extractor.ExtractFromGp(@"..\..\..\gp\test15.gp");
            
            var newBitmap2 = new Bitmap(700, 700);
            
            var pixelSize = 2;

            var layout = extractResult.LayoutCollection.Last();
            var imageBytes = extractResult.LayoutCollection.Last().Bytes.Skip(23).ToArray();
            int z1 = 0;
            var bytesInBlock = 3;

            var piactureElements = new Collection<MultiPictureEl>();

            var rowIndex = 0;
            
            for (var i = 0; i < imageBytes.Length - 5; i += bytesInBlock)
            {
                int blockType = imageBytes[i];
                
                    switch (blockType)
                    {
                        case 0: bytesInBlock = 11; break;
                        case 1: bytesInBlock = 3; break;
                        case 0xd0: bytesInBlock = 16; break;
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
                            length = 1, //todo resolve
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

            using (var graphics = Graphics.FromImage(newBitmap2))
            {
                graphics.FillRectangle(new SolidBrush(Color.SpringGreen), new Rectangle(0,0,newBitmap2.Width,newBitmap2.Height));
            }

            foreach (var it in piactureElements)
            {
                var color = Color.Black;

                var brush = new SolidBrush(color);

                var offsetx = 0;
                
                foreach (var block in it.Collection)
                {
                    offsetx += block.offsetx;
                    
                    using (var graphics = Graphics.FromImage(newBitmap2))
                    {
                        graphics.FillRectangle(brush, new Rectangle(new Point(offsetx * pixelSize, it.RowIndex * pixelSize), new Size(block.length * pixelSize, pixelSize)));
                    }
                    offsetx += block.length;

                }
            }

            pictureBox1.Image = getBitmap(@"..\..\..\palette\0\OLD.PAL");
            pictureBox2.Image = newBitmap2;
        }

        private Bitmap getBitmap(string path)
        {
            var paletteBytes = File.ReadAllBytes(path);

            var newBitmap = new Bitmap(500, 500);

            var z = 0;
            var y = 5;

            var colorCollection = new Collection<Color>();

            for (int i = 0; i < paletteBytes.Length - 2; i += 3)
            {
                colorCollection.Add(Color.FromArgb(255, paletteBytes[i], paletteBytes[i + 1], paletteBytes[i + 2]));
            }

            var pixelSize = 10;

            foreach (var color in colorCollection)
            {
                var brush = new SolidBrush(color);

                using (var graphics = Graphics.FromImage(newBitmap))
                {
                    graphics.FillRectangle(brush, new Rectangle(new Point(z, y), new Size(pixelSize, pixelSize)));
                }

                z += pixelSize;

                if (z >= 100 * pixelSize)
                {
                    z = 0;
                    y += pixelSize;
                }

            }

            return newBitmap;
        }
    }
}
