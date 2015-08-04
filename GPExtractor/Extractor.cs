using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GPExtractor
{
    public struct Block
    {
        public int offsetx { get; set; }
        public int length { get; set; }
    }

    [DebuggerDisplay("{RowIndex} {Collection.Count}")]
    public class MultiPictureEl
    {
        public int RowIndex { get; set; }
        public Collection<Block> Collection { get; set; }

        public MultiPictureEl()
        {
            Collection = new Collection<Block>();
        }

        public MultiPictureEl(Collection<Block> collection)
        {
            Collection = collection;
        }
    }
    
    public class Extractor
    {
        private readonly string _logPath;

        public Extractor(string logPath)
        {
            _logPath = logPath;
        }

        private ImageLayoutInfo readImageLayoutInfo(byte[] bytes, uint offset)
        {
            CheckItem(ref bytes, offset);

            var layoutInfo = new ImageLayoutInfo
            {
                newImageOffset = BitConverter.ToInt32(new[] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] }, 0),
                offsetY = BitConverter.ToInt16(new[] { bytes[offset + 4], bytes[offset + 5] }, 0),
                offsetX = BitConverter.ToInt16(new[] { bytes[offset + 6], bytes[offset + 7] }, 0),
                Width = BitConverter.ToInt16(new[] { bytes[offset + 8], bytes[offset + 9] }, 0),
                Height = BitConverter.ToInt16(new[] { bytes[offset + 10], bytes[offset + 11] }, 0),
                NumberOfRows = BitConverter.ToInt16(new[] { bytes[offset + 21], bytes[offset + 22] }, 0),
                ByteOffset = offset,
            };
            
            if (layoutInfo.newImageOffset > -1)
            {
                layoutInfo.Bytes = bytes.Skip((int) offset).Take(layoutInfo.newImageOffset - 1).ToArray();
            }
            else
            {
                layoutInfo.Bytes = bytes.Skip((int) offset).ToArray();
            }

            for (int i = 0; i < layoutInfo.Bytes.Length; i += 1)
            {
                if (layoutInfo.Bytes[i] == 17)
                {
                 //   Debug.WriteLine(i + " - " + layoutInfo.Bytes[i - 1] + " :: " + layoutInfo.Bytes[i + 1]);
                }
            }

            return layoutInfo;
        }

        public ExtractorResult ExtractFromGp(string path)
        {
            var fullPath = Path.GetFullPath(path);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(path);
            }

            var bytes = File.ReadAllBytes(fullPath);

            CheckSignature(bytes);

            var numberOfFiles = GetNumberOfFiles(ref bytes);
            var layoutInfoCollection = new Collection<ImageLayoutInfo>();

            int z = 0xE;
            
            uint prevoffset = 0;
            int z_ = 0;
            uint offset_ = 0;
            uint lastOffset = 0;
            for (int i = 0; i < numberOfFiles; i++)
            {
                var offset = BitConverter.ToUInt32(new[] { bytes[z], bytes[z + 1], bytes[z + 2], bytes[z+3] }, 0);

                var str = String.Format("{0:X2} {1:X2} {2:X2} {3:X2} |\n", bytes[z], bytes[z + 1], bytes[z + 2],
                    bytes[z + 3]);


                var layoutInfo = readImageLayoutInfo(bytes, offset);
                layoutInfoCollection.Add(layoutInfo);

                if (i == 0)
                {
                    offset_ = offset;
                }

                if (layoutInfo.newImageOffset > -1)
                {
                    var newImageLayoutInfo = readImageLayoutInfo(bytes, (uint)(offset + layoutInfo.newImageOffset));

                    layoutInfoCollection.Add(newImageLayoutInfo);
                }

//                for (var k = 0; k < 1000; k++)
//                {
//                    str += String.Format("{0:X2} ", bytes[offset + k]);
//                }
//                str += "\n";
                
            Debug.Write(str);
                File.AppendAllText(_logPath, str);
                z += 4;
                z_ = z;

            }

            //var firstimageBytes = bytes.Skip((int)layoutInfoCollection.First().ByteOffset).ToArray();

            var cTable = new Collection<byte>();

            for (int i = z_; i < offset_; i++)
            {
                cTable.Add(bytes[i]);    
            }

            File.AppendAllText(_logPath, "\n");

            return new ExtractorResult
            {
                PaletteBytes = cTable.ToArray(),
                //ImageBytes = firstimageBytes,
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
