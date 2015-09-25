using System;
using System.Collections.Generic;
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

        public static bool Equals(MultiPictureEl one, MultiPictureEl two)
        {
            return one.Collection.SequenceEqual(two.Collection);
        }
    }

    [DebuggerDisplay("{Values}")]
    public class CounterSection
    {
        //public int Counter { get { return SecondPartBlocks.Count; } }

        public int Row { get; set; }

        public CounterSection(List<CounterBlock> secondPartBlocks)
        {
            SecondPartBlocks = secondPartBlocks;
        }

        public string Values
        {
            get { return String.Join(" ", SecondPartBlocks.Select(it => String.Format("{0:X2}", it.Offset)).ToArray()); }
        }

        public List<CounterBlock> SecondPartBlocks;
    }

    [DebuggerDisplay("{One} {Two}")]
    public class CounterBlock
    {
        //public int Index { get; set; }

        private byte One { get; set; }
        private byte Two { get; set; }

        public int Offset
        {
            get { return (One | (FourthOcted << 8)); }
        }

        public byte ThirdOcted
        {
            get { return (byte)((Two) >> 4); }
        }

        private byte FourthOcted
        {
            get { return (byte)((Two) & 0x0f); }
        }

        public CounterBlock(int index, byte one, byte two)
        {
            //Index = index;
            One = one;
            Two = two;
        }
    }
}