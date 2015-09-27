namespace Types
{
    public class CounterBlock
    {
        private readonly byte _one;
        private readonly byte _two;

        public byte FourthOctet { get; private set; }
        public int Offset { get; private set; }
        public byte ThirdOctet { get; private set; }

        public CounterBlock(byte one, byte two)
        {
            _one = one;
            _two = two;
            ThirdOctet = (byte) ((two) >> 4);
            FourthOctet = (byte)((two) & 0x0f); 
            Offset = (one | ((two) & 0x0f) << 8); 
        }
    }
}