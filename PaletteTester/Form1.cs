using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
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

            for (int i = 0; i < 32; i++)
            {
                Debug.WriteLine("{0:X2} {1:X2} {2:X2}", gBytes[i], bBytes[i], gBytes[i]);
            }
        }
    }
}
