using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PaletteTester
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var service = new PaletteReader.PaletteProcessor();

            var gBytes = service.ReadSetFiles(@"..\..\..\palette\0\g.set");
            var bBytes = service.ReadSetFiles(@"..\..\..\palette\0\b.set");
            var gbBytes = service.ReadSetFiles(@"..\..\..\palette\0\gb.set");

           
            var paletteBytes = File.ReadAllBytes(@"..\..\..\palette\0\old.pal");

            pictureBox1.Image = getBitmap(paletteBytes);
        }

        private Bitmap getBitmap(byte[] paletteBytes)
        {
            var newBitmap = new Bitmap(500, 500);

            var z = 0;
            var y = 5;

            var colorCollection = new Collection<Color>();

            for (int i = 0; i < paletteBytes.Length - 2; i += 3)
            {
                colorCollection.Add(Color.FromArgb(255, paletteBytes[i], paletteBytes[i + 1], paletteBytes[i + 2]));

                if (paletteBytes[i] == 0xfc && paletteBytes[i+1] == 0x00 && paletteBytes[i+2] == 0x00)
                {
                    var h = 5;
                }
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
