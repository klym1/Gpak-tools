using System;

namespace GPExtractor
{
    public class ByteLayoutAttribute : Attribute
    {
        public int Offset { get; private set; }
       
        public ByteLayoutAttribute(int offset)
        {
            Offset = offset;
        }
    }
}