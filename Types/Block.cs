using System.Diagnostics;

namespace Types
{
    [DebuggerDisplay("{offsetx} {length}")]
    public struct Block
    {
        public int offsetx { get; set; }
        public int length { get; set; }

        public bool Equals(Block other)
        {
            return offsetx == other.offsetx && length == other.length;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Block && Equals((Block) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (offsetx*397) ^ length;
            }
        }
    }
}