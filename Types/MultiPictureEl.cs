using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace Types
{
    [DebuggerDisplay("{RowIndex} {Collection.Count}")]
    public class MultiPictureEl
    {
        public int RowIndex { get; set; }
        public Collection<Block> Collection { get; set; }

        public MultiPictureEl()
        {
            Collection = new Collection<Block>();
        }

        public MultiPictureEl(Collection<Block> collection)
        {
            Collection = collection;
        }

        public static bool Equals(MultiPictureEl one, MultiPictureEl two)
        {
            return one.Collection.SequenceEqual(two.Collection);
        }
    }
}