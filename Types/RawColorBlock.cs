namespace Types
{
    public enum RawColorBlockType
    {
        SinglePixel,
        MultiPiexl
    }

    public class RawColorBlock
    {
        private readonly byte _one;
        private readonly byte _two;

        public int Offset { get; private set; }
        public byte ThirdOctet { get; private set; }
        public RawColorBlockType type;

        public RawColorBlock(RawColorBlockType typee, byte one, byte two)
        {
            _one = one;
            _two = two;
            ThirdOctet = (byte) ((two) >> 4);
            Offset = (one | ((two) & 0x0f) << 8);
            type = typee;
        }
    }
}