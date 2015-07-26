using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaletteReader
{
    public class PaletteProcessor
    {
        public void ReadSetFiles(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var fileName = Path.GetFileName(path);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(path);
            }

            var bytes = File.ReadAllBytes(path);

            var collection = new Collection<byte>();

            for (int i = 0; i < 32; i++)
            {
                

            }
        }
    }
}
