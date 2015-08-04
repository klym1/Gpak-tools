using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaletteReader
{
    public class PaletteProcessor
    {


        public byte [] ReadSetFiles(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var fileName = Path.GetFileName(path);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(path);
            }

            var bytes = File.ReadAllBytes(path);

            var collection = new Collection<byte>();
            var rowSize = 8;
            var rowsNumber = bytes.Length/rowSize;

            var collectionOfBytes = new Collection<byte>();

            for (int i = 0; i < rowsNumber; i ++)
            {
                var str = string.Empty;
                for (var offset = 0; offset < rowSize; offset++)
                {
                    str += String.Format("{0:X2} ", bytes[i*rowSize + offset]);
                }

                collectionOfBytes.Add(FromBits(bytes[i*rowSize],
                    bytes[i * rowSize+1],
                    bytes[i * rowSize + 2],
                    bytes[i * rowSize + 3],
                    bytes[i * rowSize + 4],
                    bytes[i * rowSize + 5],
                    bytes[i * rowSize + 6],
                    bytes[i * rowSize + 7]
                    ));

                Debug.WriteLine(str);
                
            }

            return collectionOfBytes.ToArray();
        }

        private byte FromBits(byte _1, byte _2, byte _3, byte _4, byte _5, byte _6, byte _7, byte _8)
        {
            var number = new byte();

            number += (byte)((_8) << 0);
            number += (byte)((_7) << 1);
            number += (byte)((_6) << 2);
            number += (byte)((_5) << 3);
            number += (byte)((_4) << 4);
            number += (byte)((_3) << 5);
            number += (byte)((_2) << 6);
            number += (byte)((_1) << 7);

            return number;
        }
    }
}
