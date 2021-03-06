﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using Types;

namespace GPExtractor
{
    public class ImageGenerator
    {
        public void RenderCounterBlocksOnBitmap(ImageView imageView, NationColorOffset nationColorOffset, List<AbsoluteBlock> piactureElements, Collection<RawColorBlock> secondPartBlocks, ImageLayoutInfo layout, Color[] imagePaletteArray, Color[] generalPaletteColors)
        {
            var blocksDistributor = new BlocksDistributor();

           var blockContainerCollection = Helper.WithMeasurement(() =>
             blocksDistributor.GetDistributedCounterPartBlocks(piactureElements, secondPartBlocks), "GetDistributedCounterPartBlocks");

            foreach (var blockContainer in blockContainerCollection)
            {
                foreach (var counterBlockContainer in blockContainer.CounterBlockContainers)
                {
                    var sourceOffset = counterBlockContainer.RawColorBlock.Offset +
                                          counterBlockContainer.StripePadding;

                    if (counterBlockContainer.RawColorBlock.type == RawColorBlockType.MultiPixel)
                    {
                        var offsetX = layout.offsetX + blockContainer.Block.OffsetX + counterBlockContainer.Offset;
                        var offsetY = layout.offsetY + blockContainer.Block.OffsetY;
                        var destinationOffset = offsetY * imageView.Width + offsetX;
                        
                        Array.Copy(
                            sourceArray: imagePaletteArray,
                            sourceIndex: sourceOffset,
                            destinationArray: imageView.Pixels,
                            destinationIndex: destinationOffset,
                            length: counterBlockContainer.Width);

                    }
                    else if (counterBlockContainer.RawColorBlock.type == RawColorBlockType.FourPixel)
                    {

                        var offsetX = layout.offsetX + blockContainer.Block.OffsetX + counterBlockContainer.Offset;
                        var offsetY = layout.offsetY + blockContainer.Block.OffsetY;
                        var destinationOffset = offsetY * imageView.Width + offsetX;

                        var colorByte = counterBlockContainer.RawColorBlock.One; // Codes 4 pixels: 2 bits for pixel

                        var _1 = colorByte & 0x03;
                        var _2 = (colorByte >> 2) & 0x03;
                        var _3 = (colorByte >> 4) & 0x03;
                        var _4 = (colorByte >> 6) & 0x03;

                        var slice = new[]
                        {
                             generalPaletteColors[nationColorOffset.Offset + _1], 
                             generalPaletteColors[nationColorOffset.Offset + _2], 
                             generalPaletteColors[nationColorOffset.Offset + _3], 
                             generalPaletteColors[nationColorOffset.Offset + _4]
                        };

                        Array.Copy(
                            sourceArray: slice,
                            sourceIndex: sourceOffset,
                            destinationArray: imageView.Pixels,
                            destinationIndex: destinationOffset,
                            length: counterBlockContainer.Width);
                    }
                    else
                    {
                        var colorIndex = counterBlockContainer.RawColorBlock.One;
                        var color = generalPaletteColors[colorIndex];
                        
                        imageView.DrawColorPixel(color, 
                           layout.offsetX + blockContainer.Block.OffsetX + counterBlockContainer.Offset,
                           layout.offsetY + blockContainer.Block.OffsetY);
                    }
                }
            }
        }

        private readonly Color _shadowColor = Color.FromArgb(0x99, 0, 0, 0);

        public void RenderShapeBlocks(ImageView imageView, List<AbsoluteBlock> piactureElements,
            ImageLayoutInfo layout)
        {
            foreach (var block in piactureElements)
            {
                var slice = Enumerable.Range(1, block.Length).Select(it => _shadowColor).ToArray();

                imageView.DrawHorizontalColorLine(slice,
                          layout.offsetX + block.OffsetX,
                          layout.offsetY + block.OffsetY);
            }
        }

        public static Color[] OffsetsToColors(byte[] imagePaletteOffsets, Color[] colorCollection)
        {
            return imagePaletteOffsets.Select(offset => colorCollection[offset]).ToArray();
        }
    }
}
