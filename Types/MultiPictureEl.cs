using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace Types
{
    [DebuggerDisplay("{RowIndex} {Collection.Count}")]
    public class RawShapeBlocksGroup
    {
        public int RowIndex { get; private set; }
        public List<RawShapeBlock> Collection { get; private set; }

        public RawShapeBlocksGroup(Collection<RawShapeBlock> collection, int rowIndex)
        {
            RowIndex = rowIndex;
            Collection = collection.ToList();
        }

        public static bool Equals(RawShapeBlocksGroup one, RawShapeBlocksGroup two)
        {
            return one.Collection.SequenceEqual(two.Collection);
        }
    }
}