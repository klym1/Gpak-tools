﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Types;

namespace GPExtractor
{
    public class Extractor
    {
        private readonly IBinaryMapper _mapper;
        private const int ImageHeaderSize = 23;

        public Extractor()
        {
            _mapper = new BinaryAutoMapper();
        }

        private ImageLayoutInfo ReadImageLayoutInfo(byte[] bytes, uint offset)
        {
            var layoutInfo = _mapper.GetMappedObject<ImageLayoutInfo>(bytes, offset);

            CheckItem(layoutInfo);

            layoutInfo.HeaderBytes = bytes.Skip((int)offset).Take(ImageHeaderSize).ToArray();
            layoutInfo.GlobalByteOffsetStart = offset;
            
            if (layoutInfo.newImageOffset > -1)
            {
                layoutInfo.Bytes = bytes
                    .Skip((int) offset)
                    .Skip(ImageHeaderSize)
                    .Take(layoutInfo.newImageOffset - 1 - ImageHeaderSize).ToArray();

                layoutInfo.GlobalByteOffsetEnd = offset + layoutInfo.newImageOffset - 1;
            }
            else
            {
                layoutInfo.Bytes = bytes
                    .Skip((int) offset)
                    .Skip(ImageHeaderSize)
                    .ToArray();

                layoutInfo.GlobalByteOffsetEnd = bytes.Length - 1;
            }

            return layoutInfo;
        }

        private Collection<ImageLayoutInfo> ExtractFromGp(string path)
        {
            var fullPath = Path.GetFullPath(path);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(path);
            }

            var bytes = File.ReadAllBytes(fullPath);

            CheckSignature(bytes);

            var numberOfFiles = GetNumberOfFiles(ref bytes);
            var layoutInfoCollection = new Collection<ImageLayoutInfo>();

            int z = 0xE;

            for (int i = 0; i < numberOfFiles; i++)
            {
                var offset = BitConverter.ToUInt32(new[] { bytes[z], bytes[z + 1], bytes[z + 2], bytes[z+3] }, 0);
                
                var layoutInfo = ReadImageLayoutInfo(bytes, offset);
                layoutInfoCollection.Add(layoutInfo);

                //Handle more than two images
                if (layoutInfo.newImageOffset > -1)
                {
                    var newImageLayoutInfo = ReadImageLayoutInfo(bytes, (uint)(offset + layoutInfo.newImageOffset));

                    layoutInfo.ChildImageLayoutInfo = newImageLayoutInfo;
                    layoutInfoCollection.Add(newImageLayoutInfo);

                }
          
                z += 4;
            }

            return layoutInfoCollection;
        }

        public IList<ImageLayout> GetImagesFromOutput(string path)
        {
            var extractResult = ExtractFromGp(path);

            var imageLayoutCollection = new Collection<ImageLayout>();
            var currentImageLayout = new ImageLayout();

            foreach (var imageLayoutInfo in extractResult)
            {
                currentImageLayout.PartialLayouts.Add(imageLayoutInfo);

                if (imageLayoutInfo.ChildImageLayoutInfo == null)
                {
                    imageLayoutCollection.Add(currentImageLayout);
                    currentImageLayout = new ImageLayout();

                }
            }

            return imageLayoutCollection;
        } 

        private void CheckItem(ImageLayoutInfo layoutInfo)
        {
            if (layoutInfo.EndOfHeader != -1)
            {
                throw new Exception("EndOfHeader should be FF FF FF FF");
            }
        }

        private int GetNumberOfBytesForColorPallete(ref byte[] bytes)
        {
            return BitConverter.ToInt16(new[] {bytes[0xc], bytes[0xd]}, 0);
        }

        private int GetNumberOfFiles(ref byte[] bytes)
        {
            return bytes[4];
        }

        private void CheckSignature(byte[] bytes)
        {
            if (!new byte[] { 0x47, 0x50, 0x41, 0x4B }.SequenceEqual(new [] { bytes[0], bytes[1], bytes[2], bytes[3] }))
            {
                throw new Exception("File has wrong structure");
            }
        }

        public byte[] GetPaletteBytes(string path)
        {
            var fullPath = Path.GetFullPath(path);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(path);
            }

            var bytes = File.ReadAllBytes(fullPath);

            var cTable = new Collection<byte>();

            var numberOfBytesForColorPalette = GetNumberOfBytesForColorPallete(ref bytes);
            // CheckPaletteOffset(offset_ - z_, numberOfBytesForColorPalette);

            var palletteBytesOffsetStart = BitConverter.ToUInt16(new[] { bytes[8], bytes[9] }, 0);

            var numberOfFiles = GetNumberOfFiles(ref bytes);
            var layoutInfoCollection = new Collection<ImageLayoutInfo>();

            int z = 0xE;
            int z_ = 0;
            uint offset_ = 0;
            for (int i = 0; i < numberOfFiles; i++)
            {
                var offset = BitConverter.ToUInt32(new[] { bytes[z], bytes[z + 1], bytes[z + 2], bytes[z + 3] }, 0);

                if (i == 0)
                {
                    offset_ = offset;
                }

                z += 4;
                z_ = z;
            }

            for (int i = z_; i < offset_; i++)
            {
                cTable.Add(bytes[i]);
            }

//            for (int i = palletteBytesOffsetStart; i < numberOfBytesForColorPalette; i++)
//            {
//                cTable.Add(bytes[i]);
//            }

            return cTable.ToArray();
        }
    }

    public class ImageLayout
    {
        public ICollection<ImageLayoutInfo> PartialLayouts { get; set; }

        public ImageLayout()
        {
            PartialLayouts = new List<ImageLayoutInfo>();
        }
    }
}
