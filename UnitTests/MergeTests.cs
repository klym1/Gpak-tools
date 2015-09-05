using System.Collections.ObjectModel;
using FluentAssertions;
using NUnit.Framework;
using Types;

namespace UnitTests
{
    [TestFixture]
    public class MergeTests
    {
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