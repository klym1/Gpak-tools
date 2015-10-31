using System;

namespace Types
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