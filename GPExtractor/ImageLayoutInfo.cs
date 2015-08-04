using System.Collections.ObjectModel;

namespace GPExtractor
{
    public class ImageLayoutInfo
    {
        public int offsetX { get; set; }
        public int offsetY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public short NumberOfRows { get; set; }
        public uint ByteOffset { get; set; }
        
        public int newImageOffset { get; set; }
        public byte[] Bytes { get; set; }
    }
}