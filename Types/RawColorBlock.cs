namespace Types
{
    public enum RawColorBlockType
    {
        SinglePixel,
        MultiPixel,
        FourPixel
    }

    public class RawColorBlock
    {
        public byte One { get; private set; }
        public byte Two { get; private set; }

        public int Offset { get; private set; }
        public byte ThirdTetrade { get; private set; } // four-bit group
        public RawColorBlockType type;

        public RawColorBlock(RawColorBlockType typee, byte one, byte two)
        {
            One = one;
            Two = two;
            ThirdTetrade = (byte) ((two) >> 4);
            Offset = (one | ((two) & 0x0f) << 8);
            type = typee;
        }

        public RawColorBlock(RawColorBlockType typee, byte one)
        {
            One = one;
            type = typee;
        }
    }
}