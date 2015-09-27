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

            Collection = collection.Select(it => new Block
            {
                Length = it.Length,
                Offsetx = it.Offsetx,
                OffsetY = RowIndex
            }).ToList();
        }

        public static bool Equals(MultiPictureEl one, MultiPictureEl two)
        {
            return one.Collection.SequenceEqual(two.Collection);
        }
    }

//    [DebuggerDisplay("{Values}")]
//    public class CounterSection
//    {
//        public int Row { get; set; }
//
//        public CounterSection(List<CounterBlock> secondPartBlocks)
//        {
//            SecondPartBlocks = secondPartBlocks;
//        }
//
//        public string Values
//        {
//            get { return String.Join(" ", SecondPartBlocks.Select(it => String.Format("{0:X2}", it.Offset)).ToArray()); }
//        }
//
//        public List<CounterBlock> SecondPartBlocks;
//    }

    public class CounterBlock
    {
        private readonly byte _one;
        private readonly byte _two;

        public int Offset
        {
            get { return (_one | ((_two) & 0x0f) << 8); }
        }

        //Always == 0xD //?
        private byte ThirdOcted
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
        public CounterBlock counterBlock;
        public int Width;

        public CounterBlockContainer(CounterBlock counterBlock, int width)
        {
            this.counterBlock = counterBlock;
            Width = width;
        }
    }
}