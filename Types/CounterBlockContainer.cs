namespace Types
{
    public class CounterBlockContainer
    {
        public RawColorBlock RawColorBlock { get; private set; }
        public int Width { get; private set; }
        public int Offset { get; private set; }
        public int StripePadding { get; private set; }
        
        public CounterBlockContainer( RawColorBlock rawColorBlock, int width, int offset, int stripePadding)
        {
            RawColorBlock = rawColorBlock;
            Width = width;
            Offset = offset;
            StripePadding = stripePadding;
        }
    }
}