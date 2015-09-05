using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;

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
    }

    public class MultiPictureElGroup
    {
        public int GroupIndex { get; set; }
        public Collection<Block> FirstColumnBlocks { get; set; }

        public int Length {get { return FirstColumnBlocks.First().length; }}

        public MultiPictureElGroup()
        {
            FirstColumnBlocks = new Collection<Block>();
        }

        
    }
}