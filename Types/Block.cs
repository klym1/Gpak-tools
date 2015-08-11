using System.Diagnostics;

namespace Types
{
    [DebuggerDisplay("{offsetx} {length}")]
    public struct Block
    {
        public int offsetx { get; set; }
        public int length { get; set; }
    }
}