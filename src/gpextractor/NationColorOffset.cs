namespace GPExtractor
{
    public struct NationColorOffset
    {
        public byte Offset { get; set; }

        public NationColorOffset(byte offset) : this()
        {
            Offset = offset;
        }

        public static readonly NationColorOffset Red = new NationColorOffset(0xd0);
        public static readonly NationColorOffset Blue = new NationColorOffset(0xd4);
        public static readonly NationColorOffset Green = new NationColorOffset(0xd8);
        public static readonly NationColorOffset Purple = new NationColorOffset(0xdC);
        public static readonly NationColorOffset Orange = new NationColorOffset(0xe0);
        public static readonly NationColorOffset Gray = new NationColorOffset(0xe4);
        
    }
}