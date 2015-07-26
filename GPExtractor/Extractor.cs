using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GPExtractor
{
    public class Extractor
    {
        private readonly string _logPath;

        public Extractor(string logPath)
        {
            _logPath = logPath;
        }

        public ExtractorResult ExtractFromGp(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var fileName = Path.GetFileName(path);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(path);
            }

            var bytes = File.ReadAllBytes(fullPath);

            CheckSignature(bytes);

            var numberOfFiles = GetNumberOfFiles(ref bytes);
            var numberOfBytesForPalette = GetNumberOfBytesForColorPallete(ref bytes);
            var layoutInfoCollection = new Collection<ImageLayoutInfo>();

            int z = 0xE;
            
            uint prevoffset = 0;
            int z_ = 0;
            uint offset_ = 0;
            uint lastOffset = 0;
            for (int i = 0; i < numberOfFiles; i++)
            {
                var offset = BitConverter.ToUInt32(new[] { bytes[z], bytes[z + 1], bytes[z + 2], bytes[z+3] }, 0);
                var str = String.Format("{0:X2} {1:X2} {2:X2} {3:X2} | {4:D5} | {5:D5}|", bytes[z], bytes[z + 1], bytes[z + 2],
                    bytes[z + 3], (i==0 ? 0 : offset - prevoffset), BitConverter.ToInt16(new byte[]{bytes[offset], bytes[offset+1]},0));
                lastOffset = offset;

                var layoutInfo = new ImageLayoutInfo
                {
                    offsetY = BitConverter.ToInt16(new []{bytes[offset+4], bytes[offset+5]},0),
                    offsetX = BitConverter.ToInt16(new []{bytes[offset+6], bytes[offset+7]},0),
                    Width = BitConverter.ToInt16(new []{bytes[offset+8], bytes[offset+9]},0),
                    Height = BitConverter.ToInt16(new []{bytes[offset+10], bytes[offset+11]},0),
                    NumberOfRows = BitConverter.ToInt16(new []{bytes[offset+21], bytes[offset+22]},0)
                };

                for (uint j = offset + 23; j < offset + 23 + 5*layoutInfo.NumberOfRows; j+=5)
                {
                    layoutInfo.RowInfos.Add(new RowInfo
                    {
                        A = bytes[j],
                        B = bytes[j+1],
                        C = bytes[j+2],
                        D = bytes[j+3],
                        E = bytes[j+4],
                    });
                }

                layoutInfoCollection.Add(layoutInfo);

                if (i == 0)
                {
                    offset_ = offset;
                }

                for (var k = 0; k < 1000; k++)
                {
                    str += String.Format("{0:X2} ", bytes[offset + k]);
                }
                str += "\n";

                CheckItem(ref bytes, offset);

            Console.Write(str);
                File.AppendAllText(_logPath, str);
                z += 4;
                prevoffset = offset;
                z_ = z;

            }

            var lastimageBytes = bytes.Skip((int) lastOffset).ToArray();

            var cTable = new Collection<byte>();

            //Console.WriteLine("______________________");
            //File.AppendAllText(_logPath, "______________________\n");
            var output = string.Empty;
            for (int i = z_; i < offset_; i++)
            {
                cTable.Add(bytes[i]);    
            }

            File.AppendAllText(_logPath, "\n");

            return new ExtractorResult
            {
                PaletteBytes = cTable.ToArray(),
                ImageBytes = lastimageBytes,
                LayoutCollection = layoutInfoCollection
            };
        }

        private void CheckItem(ref byte[] bytes, uint offset)
        {
            var h = true
                    && bytes[offset + 12] == 0xff
                    && bytes[offset + 13] == 0xff
                    && bytes[offset + 14] == 0xff
                    && bytes[offset + 15] == 0xff;

            if (!h)
            {
                var j = 6;
            }
        }

        private int GetNumberOfBytesForColorPallete(ref byte[] bytes)
        {
            return BitConverter.ToInt16(new byte[] {bytes[0xc], bytes[0xd]}, 0);
        }

        private int GetNumberOfFiles(ref byte[] bytes)
        {
            return bytes[4];
        }

        private void CheckSignature(byte[] bytes)
        {
            if (!new byte[] { 0x47, 0x50, 0x41, 0x4B }.SequenceEqual(new [] { bytes[0], bytes[1], bytes[2], bytes[3] }))
            {
                throw new Exception("File has wrong structure");
            }
        }
    }
}
