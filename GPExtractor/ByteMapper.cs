using System;
using System.IO;
using System.Linq;
using Types;

namespace GPExtractor
{
    public interface IBinaryMapper
    {
        T GetMappedObject<T>(byte[] bytes, uint initialOffset) where T : class, new();
    }
    
    public class BinaryManualMapper : IBinaryMapper
    {
        public T GetMappedObject<T>(byte[] bytes, uint initialOffset) where T : class,new()
        {
            if (typeof (T) == typeof (ImageLayoutInfo))
            {
                var offset = initialOffset;

                var layoutInfo = new ImageLayoutInfo
                {
                    newImageOffset =
                        BitConverter.ToInt32(
                            new[] {bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]}, 0), //sometimes it is FF FF FF FF, sometimes - not
                    offsetY = BitConverter.ToInt16(new[] {bytes[offset + 4], bytes[offset + 5]}, 0),
                    offsetX = BitConverter.ToInt16(new[] {bytes[offset + 6], bytes[offset + 7]}, 0),
                    Width = BitConverter.ToInt16(new[] {bytes[offset + 8], bytes[offset + 9]}, 0),
                    Height = BitConverter.ToInt16(new[] {bytes[offset + 10], bytes[offset + 11]}, 0),
                    EndOfHeader = BitConverter.ToInt32(new[] {bytes[offset + 12], bytes[offset + 13], bytes[offset + 14], bytes[offset + 15]}, 0), // should always be FF FF FF FF
                    NumberOfRows = BitConverter.ToInt16(new[] {bytes[offset + 21], bytes[offset + 22]}, 0),
                };

                return layoutInfo as T;
            }

            var emptyObject = Activator.CreateInstance<T>();
            return emptyObject;
        }
    }

    public class BinaryAutoMapper : IBinaryMapper
    {
        public T GetMappedObject<T>(byte[] bytes, uint initialOffset) where T: class, new()
        {
            var emptyObject = Activator.CreateInstance<T>();

            var allproperties =
                emptyObject.GetType()
                    .GetProperties()
                    .Where(it => it.CustomAttributes.Any(o => o.AttributeType == typeof (ByteLayoutAttribute)))
                    .ToList();

            using (var memoryStream = new MemoryStream(bytes))
            using (var binaryReader = new BinaryReader(memoryStream))
            {
                memoryStream.Position = initialOffset;

                foreach (var propertyInfo in allproperties)
                {
                    var attributeData =
                        (ByteLayoutAttribute) Attribute.GetCustomAttribute(propertyInfo, typeof (ByteLayoutAttribute));

                    if (attributeData != null)
                    {
                        var proprtyType = propertyInfo.PropertyType;
                        memoryStream.Position = initialOffset + attributeData.Offset;

                        object value = null;

                        if (proprtyType == typeof (Int16))
                        {
                            value = binaryReader.ReadInt16();
                           
                        } else if (proprtyType == typeof (Int32))
                        {
                            value = binaryReader.ReadInt32();
                        }
                        else
                        {
                            throw new Exception("Unsupported type");
                        }

                        propertyInfo.SetValue(emptyObject, value);
                    }
                }
            }
            return emptyObject;
        }


    }
}