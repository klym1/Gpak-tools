using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
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
            
            var extractResult = extractor.ExtractFromGp(@"..\..\..\gp\test7_7.gp");

            var paletteBytes = File.ReadAllBytes(@"..\..\..\palette\0\OLD.PAL");
            
            var newBitmap = new Bitmap(500, 500);
            var newBitmap2 = new Bitmap(500, 500);

            var z = 0;
            var y = 5;
            var index = 0;

            var colorCollection = new Collection<Color>();
            
            for (int i = 0; i < paletteBytes.Length - 2; i += 3)
            {
                colorCollection.Add(Color.FromArgb(255, paletteBytes[i], paletteBytes[i + 1], paletteBytes[i + 2]));  
            }

            var imagePalleteCollection = new Collection<Color>();

            var pixelSize = 4;

            foreach (var colorIndex in extractResult.PaletteBytes)
            {
                var color = colorCollection[colorIndex];
                imagePalleteCollection.Add(color);
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

                index++;
            }

             z = 0;
             y = 10;

            pixelSize = 4;

            var layout = extractResult.LayoutCollection.Last();
            var imageBytes = extractResult.ImageBytes.Skip(16).ToArray();
            int z1 = 0;
            for( var i = 0; i<imageBytes.Length-5;i+=5 )
            {

                Debug.WriteLine("{0}. {1:X2} {2:X2} {3:X2} {4:X2} {5:X2}", z1,
                    imageBytes[i],
                    imageBytes[i+1],
                    imageBytes[i+2],
                    imageBytes[i+3],
                    imageBytes[i+4]
                    );
                var color = Color.Magenta;//imagePalleteCollection[colorIndex];

                var brush = new SolidBrush(color);

                using (var graphics = Graphics.FromImage(newBitmap2))
                {
                    graphics.FillRectangle(brush, new Rectangle(new Point(z, y), new Size(pixelSize, pixelSize)));
                }

                z += pixelSize;

                if (z > layout.Width * pixelSize)
                {
                    z = 0;
                    y += pixelSize;
                }
                z1++;
            }

            pictureBox1.Image = newBitmap;
            pictureBox2.Image = newBitmap2;
        }
    }
}
