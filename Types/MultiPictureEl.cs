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
        public int RowIndex { get; private set; }
        public List<Block> Collection { get; private set; }

        public MultiPictureEl(Collection<Block> collection, int rowIndex)
        {
            RowIndex = rowIndex;

            Collection = collection.Select(it => new Block(it.Offsetx, it.Length)).ToList();
        }

        public static bool Equals(MultiPictureEl one, MultiPictureEl two)
        {
            return one.Collection.SequenceEqual(two.Collection);
        }
    }

    [DebuggerDisplay("{Collection.Count}")]
    public class AbsoluteMultiPictureEl
    {
        public List<AbsoluteBlock> Collection { get; private set; }

        public AbsoluteMultiPictureEl(Collection<AbsoluteBlock> collection)
        {
            Collection = collection.ToList();
        }

        public static bool Equals(AbsoluteMultiPictureEl one, AbsoluteMultiPictureEl two)
        {
            return one.Collection.SequenceEqual(two.Collection);
        }
    }

    public class CounterBlock
    {

        private readonly byte _one;
        private readonly byte _two;

        public int FourthOctet
        {
            get { return (byte)((_two) & 0x0f); }
        }

        public int Offset
        {
            get { return (_one | ((_two) & 0x0f) << 8); }
        }

        public byte ThirdOctet
        {
            get { return (byte)((_two) >> 4); }
        }

        public CounterBlock(byte one, byte two)
        {
            _one = one;
            _two = two;
        }
    }

    public class CounterBlockContainer
    {
        public CounterBlock CounterBlock;
        public int Width;
        public int Offset;
        public int StripePadding;
        
        public CounterBlockContainer( CounterBlock counterBlock, int width, int offset, int stripePadding)
        {
            CounterBlock = counterBlock;
            Width = width;
            Offset = offset;
            StripePadding = stripePadding;

            if (width + stripePadding != 16)
            {
                //
            }
        }
    }
}