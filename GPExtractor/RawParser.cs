using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using Types;

namespace GPExtractor
{
    public class RawParser
    {
        public Collection<RawColorBlock> GetRawColorBlocks(byte[] imageBytes, int initialOffset)
        {
            var offset = initialOffset + 1; // skip CD bytes

            var tempByteCollection = new Collection<RawColorBlock>();

            while (offset < imageBytes.Length - 1)
            {
                var blockStartByte = imageBytes[offset];

                var blockLength = blockStartByte & 0x0f;
                var blockType = (blockStartByte >> 4);

                if (blockType != 0xf)
                {
                    throw new Exception("123");
                }

                if (blockLength < 15) // last block
                {
                    var p = imageBytes.Length - offset - 1;

                    blockLength = p > 15 ? 15 : p;
                }

                offset++;

                for (var i = 0; i < blockLength; i += 2)
                {
                    var block = new RawColorBlock(imageBytes[offset], imageBytes[offset + 1]);
                    tempByteCollection.Add(block);
                    offset += 2;
                }
            }

            return tempByteCollection;
        }

        public RawShapeBlocksGroup[] ParseRawBlockGroups(byte[] imageBytes, out int offset)
        {
            var rawShapeBlocksGroups = new Collection<RawShapeBlocksGroup>();

            var rowIndex = 0;
            offset = 0;

            //todo Improve condition
            while (imageBytes[offset] != 0xCD)
            {
                int blockType = imageBytes[offset];

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

                if (blockType > 20)
                {
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

            return rawShapeBlocksGroups.ToArray();
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