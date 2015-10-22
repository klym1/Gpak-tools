using System;

namespace Types
{
    public class ImageLayoutInfo : IEquatable<ImageLayoutInfo>
    {
        [ByteLayout(offset: 4)]
        public Int16 offsetX { get; set; }

        [ByteLayout(offset: 6)]
        public Int16 offsetY { get; set; }
        
        [ByteLayout(offset: 8)]
        public Int16 Width { get; set; }
        
        [ByteLayout(offset: 10)]
        public Int16 Height { get; set; }
        
        [ByteLayout(offset: 21)]
        public Int16 NumberOfRows { get; set; }
        
        [ByteLayout(offset: 0)]
        public Int32 newImageOffset { get; set; }
        
        [ByteLayout(offset: 12)]
        public Int32 EndOfHeader { get; set; }
        
        public byte[] HeaderBytes { get; set; }
        public byte[] Bytes { get; set; }
        public long GlobalByteOffsetEnd { get; set; }
        public uint GlobalByteOffsetStart { get; set; }

        //public ImageLayoutInfo ChildImageLayoutInfo { get; set; }

        public bool Equals(ImageLayoutInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return offsetX == other.offsetX && offsetY == other.offsetY && Width == other.Width && Height == other.Height && NumberOfRows == other.NumberOfRows && newImageOffset == other.newImageOffset && EndOfHeader == other.EndOfHeader;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ImageLayoutInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = offsetX.GetHashCode();
                hashCode = (hashCode*397) ^ offsetY.GetHashCode();
                hashCode = (hashCode*397) ^ Width.GetHashCode();
                hashCode = (hashCode*397) ^ Height.GetHashCode();
                hashCode = (hashCode*397) ^ NumberOfRows.GetHashCode();
                hashCode = (hashCode*397) ^ newImageOffset;
                hashCode = (hashCode*397) ^ EndOfHeader;
                return hashCode;
            }
        }

        public static bool operator ==(ImageLayoutInfo left, ImageLayoutInfo right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ImageLayoutInfo left, ImageLayoutInfo right)
        {
            return !Equals(left, right);
        }
    }
}