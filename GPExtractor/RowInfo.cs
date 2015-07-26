using System.Diagnostics;

namespace GPExtractor
{
    [DebuggerDisplay("{A} {B} {C} {D} {E}")]
    public class RowInfo
    {
        public byte A { get; set; }
        public byte B { get; set; }
        public byte C { get; set; }
        public byte D { get; set; }
        public byte E { get; set; }
    }
}