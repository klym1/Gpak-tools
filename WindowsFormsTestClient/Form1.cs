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
            
            var newBitmap2 = new Bitmap(500, 500);
            
            var pixelSize = 2;

            var layout = extractResult.LayoutCollection.Last();
            var imageBytes = extractResult.ImageBytes.Skip(23).ToArray();
            int z1 = 0;
            var bytesInBlock = 3;

            var blockType = 1;

            var piactureElements = new Collection<PictureEl>();

            var rowIndex = 0;

            for (var i = 0; i < imageBytes.Length - 5; i += bytesInBlock)
            {
                blockType = imageBytes[i];

                if (blockType > 1)
                {
                    switch (blockType)
                    {
                        case 0: bytesInBlock = 3; break;
                        case 1: bytesInBlock = 3; break;
                        case 0xd0: bytesInBlock = 16; break;
                        case 2: bytesInBlock = 5; break;
                        case 3: bytesInBlock = 7; break;
                        case 6: bytesInBlock = 3; break;
                    }
                    
                }

                if (blockType == 1)
                {
                    Debug.WriteLine("{0:D3}. {1:D2} {2:D2} {3:D2}", z1,
                        imageBytes[i],
                        imageBytes[i + 1],
                        imageBytes[i + 2]

                        );

                    piactureElements.Add(new PictureEl
                    {
                        offsetX = imageBytes[i + 1],
                        Length = imageBytes[i + 2],
                        RowIndex = rowIndex++
                    });
                }
                
//                else if (blockType == 3)
//                {
//                    Debug.WriteLine("{0:D3}. {1:X2} {2:X2} {3:X2} {4:X2} {5:X2} {6:X2} {7:X2}", z1,
//                  imageBytes[i],
//                  imageBytes[i + 1],
//                  imageBytes[i + 2],
//                  imageBytes[i + 3],
//                  imageBytes[i + 4],
//                  imageBytes[i + 5],
//                  imageBytes[i + 6]
//
//                       
//                  );
//                }
//
//                else if (blockType == 2)
//                {
//                    Debug.WriteLine("{0:D3}. {1:X2} {2:X2} {3:X2} {4:X2}", z1,
//                        imageBytes[i],
//                        imageBytes[i + 1],
//                        imageBytes[i + 2],
//                        imageBytes[i + 3]
//
//
//                        );
//                }
//                else if (blockType == 6)
//                {
//                    Debug.WriteLine("{0:D3}. {1:X2} {2:X2} {3:X2}", z1,
//                  imageBytes[i],
//                  imageBytes[i + 1],
//                  imageBytes[i + 2]
//
//                  );
//                }
//                else if (blockType == 0xd0)
//                {
//                    Debug.WriteLine("{0:D3}. {1:X2} {2:X2} {3:X2} {4:X2} {5:X2} {6:X2} {7:X2}", z1,
//                        imageBytes[i],
//                        imageBytes[i + 1],
//                        imageBytes[i + 2],
//                        imageBytes[i + 3],
//                        imageBytes[i + 4],
//                        imageBytes[i + 5],
//                        imageBytes[i + 6]);
//                }
//                else
//                {
//                    Debug.WriteLine("Unknow blocktype");
//                }
               
               
            }

            using (var graphics = Graphics.FromImage(newBitmap2))
            {
                graphics.FillRectangle(new SolidBrush(Color.SpringGreen), new Rectangle(0,0,newBitmap2.Width,newBitmap2.Height));
            }

            foreach (var it in piactureElements)
            {
                var color = Color.Black;

                var brush = new SolidBrush(color);

                using (var graphics = Graphics.FromImage(newBitmap2))
                {
                    graphics.FillRectangle(brush, new Rectangle(new Point(it.offsetX * pixelSize, it.RowIndex * pixelSize), new Size(it.Length * pixelSize, pixelSize)));
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
