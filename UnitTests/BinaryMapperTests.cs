using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions.Common;
using GPExtractor;
using NUnit.Framework;
using Types;

namespace UnitTests
{
    [TestFixture]
    public class BinaryMapperTests
    {
        [Test]
        public void MapperShouldMapBytesCorrectly()
        {
            var random = new Random();
            var buffer = new byte[100];
            random.NextBytes(buffer);

            var autoMapper = new BinaryAutoMapper();
            var autoMapperResult = autoMapper.GetMappedObject<ImageLayoutInfo>(buffer, 0);

            var manualMapper = new BinaryManualMapper();
            var manualMapperResult = manualMapper.GetMappedObject<ImageLayoutInfo>(buffer, 0);

            Assert.AreEqual(autoMapperResult, manualMapperResult);
        }
    }
}
