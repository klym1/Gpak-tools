using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Types;

namespace GPExtractor
{
    public class RawParser
    {
        public RawParser()
        {
            PrecalculatedValues.CalculateSecondPartBlockBits();
        }

        public Collection<RawColorBlock> GetRawColorBlocks(byte[] imageBytes, int initialOffset, int globalOffset, bool nationColorPart)
        {
            var offset = initialOffset + 1; // skip CD bytes
            globalOffset++;
            
            return nationColorPart 
                ? ProcessNataionColorPart(imageBytes, globalOffset, offset) 
                : ProcessOrdinaryPart(imageBytes, globalOffset, offset);
        }

        private Collection<RawColorBlock> ProcessOrdinaryPart(byte[] imageBytes, int globalOffset, int offset)
        {
            var tempByteCollection = new Collection<RawColorBlock>();

            while (offset < imageBytes.Length)
            {
                var blockStartByte = imageBytes[offset];

                offset++;
                globalOffset++;

                //last block might not be full (less than 8 elems)
                if (offset >= imageBytes.Length)
                {
                    break;
                }

                foreach (var bit in PrecalculatedValues.SecondPartBlockBits[blockStartByte])
                {
                    RawColorBlock block;

                    if (offset >= imageBytes.Length - 1)
                    {
                        break;
                    }

                    if (bit == 0)
                    {
                        block = ProcessSinglepixelBlock(imageBytes, offset);
                        offset++;
                        globalOffset++;
                    }
                    else
                    {
                        block = ProcessMultipixelBlock(imageBytes, offset);
                        offset += 2;
                        globalOffset += 2;
                    }

                    tempByteCollection.Add(block);
                }
            }

            return tempByteCollection;
        }

        private Collection<RawColorBlock> ProcessNataionColorPart(byte[] imageBytes, int globalOffset, int offset)
        {
            var tempByteCollection = new Collection<RawColorBlock>();

            while (offset < imageBytes.Length)
            {
                var block = ProcessFourPixelBlock(imageBytes, offset);
                tempByteCollection.Add(block);

                offset++;
                globalOffset++;
            }
            
            return tempByteCollection;
        }

        private RawColorBlock ProcessMultipixelBlock(byte[] imageBytes, int offset)
        {
            return new RawColorBlock(RawColorBlockType.MultiPixel, imageBytes[offset], imageBytes[offset + 1]);
        }

        private RawColorBlock ProcessSinglepixelBlock(byte[] imageBytes, int offset)
        {
            var @byte = imageBytes[offset];
            return new RawColorBlock(RawColorBlockType.SinglePixel, @byte);
        }

        private RawColorBlock ProcessFourPixelBlock(byte[] imageBytes, int offset)
        {
            var @byte = imageBytes[offset];
            return new RawColorBlock(RawColorBlockType.FourPixel, @byte);
        }

       public RawShapeBlocksGroup[] ParseRawBlockGroups(byte[] imageBytes, short numberOfRows, out int offset)
        {
            var rawShapeBlocksGroups = new Collection<RawShapeBlocksGroup>();

           int rowIndex = 0;
           offset = 0;

           var collectionOfBlockTypes = new Collection<byte>();

           while (rowIndex < numberOfRows)
           {
                int blockType = imageBytes[offset];

                //Debug.WriteLine("{0:X2}", blockType);

                collectionOfBlockTypes.Add((byte)blockType);

                if (blockType == 0x00)//new row
                {
                    rawShapeBlocksGroups.Add(new RawShapeBlocksGroup(new List<RawShapeBlock>
                    {
                        new RawShapeBlock(0,0)
                    }, rowIndex++));

                    offset++;
                    continue;
                }

                if (blockType >> 4 == 0xE)
                {
                    var numberOfSubBlocks = blockType & 0x0f;
                    var tempCollection = new List<RawShapeBlock>();

                    for (int i = 0; i < numberOfSubBlocks; i++)
                    {
                        var nextByte = imageBytes[offset + 1];
                        var a = (nextByte >> 4) | (1 << 4);
                        var b = (nextByte & 0xf) | (1 << 4);

                        rawShapeBlocksGroups.Add(new RawShapeBlocksGroup(new List<RawShapeBlock>
                        {
                            new RawShapeBlock(b, a)
                        }, rowIndex++));

                        tempCollection.Add(new RawShapeBlock(b, a));
                        offset++;
                    }

                    offset++;
                    rawShapeBlocksGroups.Add(new RawShapeBlocksGroup(tempCollection, rowIndex++));

                    continue;
                }

                if (blockType >> 4 == 0xC)
                {
                    var numberOfSubBlocks = blockType & 0x0f;

                    //C2 11 15 => [offset 0x11, width 1], [offset 15, width]

                    var tempCollection = new List<RawShapeBlock>();

                    for (int i = 0; i < numberOfSubBlocks; i++)
                    {
                        var nextByte = imageBytes[offset + 1];
                        var a = (nextByte >> 4);
                        var b = (nextByte & 0x0f) | (1 << 4);

                        tempCollection.Add(new RawShapeBlock(b,a));
                        offset++;

                    }
                    offset++;
                    rawShapeBlocksGroups.Add(new RawShapeBlocksGroup(tempCollection, rowIndex++));

                    continue;
                }

                if (blockType >> 4 == 0xA)
                {
                    //A1 60 => offset 0, width 22 (0x16)
                    var numberOfSubBlocks = blockType & 0x0f;

                    var tempCollection = new List<RawShapeBlock>();

                    for (int i = 0; i < numberOfSubBlocks; i++)
                    {
                        var nextByte = imageBytes[offset + 1];
                        var a = (nextByte >> 4) | (1 << 4);
                        var b = (nextByte & 0x0f);

                        tempCollection.Add(new RawShapeBlock(b, a));
                        offset++;

                    }
                    offset++;
                    rawShapeBlocksGroups.Add(new RawShapeBlocksGroup(tempCollection, rowIndex++));

                    continue;
                }

               //new
                if (blockType >> 4 == 0x7)
                {
                    var numberOfSubBlocks = blockType & 0x0f;

                    //C2 11 15 => [offset 0x11, width 1], [offset 15, width]

                    var tempCollection = new List<RawShapeBlock>();

                    for (int i = 0; i < numberOfSubBlocks; i++)
                    {
                        var nextByte = imageBytes[offset + 1];
                        var a = ((nextByte >> 4) | (0x1 << 4)) << 1;
                        var b = ((nextByte & 0xf) | (0x1 << 4)) << 1;

                        tempCollection.Add(new RawShapeBlock(b, a));
                        offset++;

                    }
                    offset++;
                    rawShapeBlocksGroups.Add(new RawShapeBlocksGroup(tempCollection, rowIndex++));

                    continue;
                }

                if (blockType >> 4 == 0x8)
                {
                    var numberOfSubBlocks = blockType & 0x0f;

                    var tempCollection = new List<RawShapeBlock>();

                    for (var i = 0; i < numberOfSubBlocks; i++)
                    {
                        var nextByte = imageBytes[offset + 1];

                        var a = (nextByte >> 4);
                        var b = (0x0f & nextByte);

                        tempCollection.Add(new RawShapeBlock(b, a));
                        offset++;
                    }

                    rawShapeBlocksGroups.Add(new RawShapeBlocksGroup(tempCollection, rowIndex++));

                    offset++;
                    continue;
                }

                //new
                if (blockType >> 4 == 0x9)
                {
                    var numberOfSubBlocks = blockType & 0x0f;

                    var tempCollection = new List<RawShapeBlock>();

                    for (var i = 0; i < numberOfSubBlocks; i++)
                    {
                        var nextByte = imageBytes[offset + 1];

                        var b = (nextByte >> 4);
                        var a = (0x0f & nextByte);

                        tempCollection.Add(new RawShapeBlock(b, a));
                        offset++;
                    }

                    rawShapeBlocksGroups.Add(new RawShapeBlocksGroup(tempCollection, rowIndex++));

                    offset++;
                    continue;
                }


               if (blockType > 111)
                {
                   //break;
                    Helper.DumpArray(imageBytes, offset - 5, 128);
                    throw new Exception("wrong block type. Dump:\n ");
                }

                //ordinary processing
                var bytesInBlock = blockType * 2 + 1;

                var emptyCollection = new List<RawShapeBlock>();

                for (int k = 0; k < blockType * 2; k += 2)
                { 
                    emptyCollection.Add(new RawShapeBlock(
                        offsetx: imageBytes[offset + k + 1],
                        length: imageBytes[offset + k + 2]));
                }

                rawShapeBlocksGroups.Add(new RawShapeBlocksGroup(emptyCollection, rowIndex++));

                offset += bytesInBlock;
            }

            var blockStatistics = collectionOfBlockTypes.OrderBy(it => it).GroupBy(it => it).ToDictionary(it => it.Key, it => it.Count());

            PrintBlocksSTatistics(blockStatistics);

            return rawShapeBlocksGroups.ToArray();
        }

        private void PrintBlocksSTatistics(Dictionary<byte, int> blockStatistics)
        {
            Console.WriteLine("\nStatistics:");
            Console.WriteLine("____________________");

            foreach (var blockStatistic in blockStatistics)
            {
                Console.WriteLine("{0:X2} - {1}", blockStatistic.Key, blockStatistic.Value);
            }
        }

        public Color[] GetColorCollectionFromPalleteFile(byte[] paletteBytes)
        {
            var colorCollection = new Collection<Color>();

            for (int i = 0; i < paletteBytes.Length - 2; i += 3)
            {
                colorCollection.Add(Color.FromArgb(255, paletteBytes[i], paletteBytes[i + 1], paletteBytes[i + 2]));
            }

            return colorCollection.ToArray();
        }
    }
}