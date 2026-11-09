namespace GPExtractor
{
    /// <summary>
    /// First of the 4 palette entries (dark to light ramp) that a player's color uses.
    /// </summary>
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
        public static readonly NationColorOffset Purple = new NationColorOffset(0xdc);
        public static readonly NationColorOffset Orange = new NationColorOffset(0xe0);
        public static readonly NationColorOffset Black = new NationColorOffset(0xe4);
        public static readonly NationColorOffset White = new NationColorOffset(0xe8);

        /// <summary>All player colors, in the order of the palette.</summary>
        public static readonly (string Name, NationColorOffset Offset)[] All =
        {
            ("Red", Red),
            ("Blue", Blue),
            ("Green", Green),
            ("Purple", Purple),
            ("Orange", Orange),
            ("Black", Black),
            ("White", White),
        };
    }
}
