using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using Types;

namespace GPExtractor
{
    public class RawParser
    {
        public Collection<RawColorBlock> GetRawColorBlocks(byte[] imageBytes, int initialOffset, int globalOffset)
        {
            var offset = initialOffset + 1; // skip CD bytes
            globalOffset++;
            var tempByteCollection = new Collection<RawColorBlock>();

            while (offset < imageBytes.Length)
            {
                var blockStartByte = imageBytes[offset];

                offset++;
                globalOffset++;

                foreach (var bit in Helper.IterateBits(blockStartByte))
                {
                    if (bit == 0)
                    {
                        ProcessSinglepixelBlock(imageBytes, offset, tempByteCollection);
                        offset++;
                        globalOffset++;
                    }
                    else
                    {
                        ProcessMultipixelBlock(imageBytes, offset, tempByteCollection);
                        offset += 2;
                        globalOffset += 2;
                    }

                    //last block might not be full (less than 8 elems)
                    if (offset == imageBytes.Length)
                    {
                        break;
                    }
                }
            }

            return tempByteCollection;
        }

        private void ProcessMultipixelBlock(byte[] imageBytes, int offset,
            Collection<RawColorBlock> tempByteCollection)
        {
            var block = new RawColorBlock(RawColorBlockType.MultiPiexl, imageBytes[offset], imageBytes[offset + 1]);
                tempByteCollection.Add(block); 
        }

        private void ProcessSinglepixelBlock(byte[] imageBytes, int offset,
           Collection<RawColorBlock> tempByteCollection)
        {
            var @byte = imageBytes[offset];
            var block = new RawColorBlock(RawColorBlockType.SinglePixel, @byte);
            tempByteCollection.Add(block);   
        }

       public RawShapeBlocksGroup[] ParseRawBlockGroups(byte[] imageBytes, out int offset)
        {
            var rawShapeBlocksGroups = new Collection<RawShapeBlocksGroup>();

            var rowIndex = 0;
            offset = 0;

            //todo Improve condition
            while (offset < imageBytes.Length )
            {
                int blockType = imageBytes[offset];

                if (imageBytes[offset + 1] == 0xFF)
                {
                    // break;
                }

                if (imageBytes[offset] == 0xCD)
                {
                    break;                
                }

                if (blockType == 0x00)//new row
                {
                    rawShapeBlocksGroups.Add(new RawShapeBlocksGroup(new List<RawShapeBlock>
                    {
                        new RawShapeBlock(0,0)
                    }, rowIndex++));

                    offset++;
                    continue;
                }

                if (blockType == 0xE1) // magic number. 1-byte coded block. Investigate other similar cases
                {
                    //E1  4B  E1  C7  => 1 27 20 1 23 28

                    var nextByte = imageBytes[offset + 1];
                    var a = (nextByte >> 4) | (1 << 4);
                    var b = (nextByte & 0xf) | (1 << 4);

                    rawShapeBlocksGroups.Add(new RawShapeBlocksGroup(new List<RawShapeBlock>
                    {
                        new RawShapeBlock(b,a)
                    }, rowIndex++));

                    offset += 2;
                    continue;
                }

                if (blockType == 0xC1)
                {
                    //C1 24 => offset 14, width 2
                    var nextByte = imageBytes[offset + 1];
                    var a = (nextByte >> 4);
                    var b = ((blockType & 0x0f) << 4) | (0x0f & nextByte);

                    rawShapeBlocksGroups.Add(new RawShapeBlocksGroup(new List<RawShapeBlock>
                    {
                        new RawShapeBlock(b,a)
                    }, rowIndex++));

                    offset += 2;
                    continue;
                }

                if (blockType == 0xC2)
                {
                    var numberOfSubBlocks = blockType & 0x0f;

                    var tempCollection = new List<RawShapeBlock>();

                    for (int i = 0; i < numberOfSubBlocks; i++)
                    {
                        var nextByte = imageBytes[offset + 1];
                        //var b = ((blockType & 0x0f) << 4) | (0x0f & nextByte);

                        tempCollection.Add(new RawShapeBlock(nextByte, 1));

                        offset++;
                    }
                    offset++;
                    rawShapeBlocksGroups.Add(new RawShapeBlocksGroup(tempCollection, rowIndex++));

                    continue;
                }

                if (blockType == 0xA1)
                {
                    //A1 60 => offset 0, width 22 (0x16)
                    var nextByte = imageBytes[offset + 1];

                    var a = (nextByte >> 4) | (1 << 4);
                    var b = (0x0f & nextByte);

                    rawShapeBlocksGroups.Add(new RawShapeBlocksGroup(new List<RawShapeBlock>
                    {
                        new RawShapeBlock(b,a)
                    }, rowIndex++));

                    offset += 2;
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

                if (blockType == 0xAA)
                {
                    //todo
                    continue;
                }

                if (blockType >> 4 == 0x2)
                {
                    offset++;
                    offset++;
                    //todo
                    continue;
                }

                if (blockType >> 4 == 0x3)
                {
                    offset++;
                    offset++;
                    //todo
                    continue;
                }
                
                if (blockType > 20)
                {
                    break;
                    Helper.DumpArray(imageBytes, offset - 5);
                    throw new Exception("wrong block type");
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

            return rawShapeBlocksGroups.MergeBlocks().ToArray();
        }

        public Collection<Color> GetColorCollectionFromPalleteFile(byte[] paletteBytes)
        {
            var colorCollection = new Collection<Color>();

            for (int i = 0; i < paletteBytes.Length - 2; i += 3)
            {
                colorCollection.Add(Color.FromArgb(255, paletteBytes[i], paletteBytes[i + 1], paletteBytes[i + 2]));
            }

            return colorCollection;
        }
    }
}