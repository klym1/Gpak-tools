using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
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

        [Test]
        public void MergeBlockHelperShouldWorkCorrectly()
        {
            var elem = new MultiPictureEl(new Collection<Block>
            {
                new Block {offsetx = 12, length = 100},
                new Block {offsetx = 0, length = 1},
                new Block {offsetx = 12, length = 50},
                new Block {offsetx = 0, length = 2},
                new Block {offsetx = 0, length = 3},
                new Block {offsetx = 1, length = 3},
            });

            var merged = elem.MergeBlocks();

            merged.Collection.ShouldBeEquivalentTo(new Collection<Block>
            {
                new Block {offsetx = 12, length = 101},
                new Block {offsetx = 12, length = 55},
                new Block {offsetx = 1, length = 3},
            }); 
        }
    }
}
