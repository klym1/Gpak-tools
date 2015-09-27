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
                new Block {Offsetx = 12, Length = 100},
                new Block {Offsetx = 0, Length = 1},
                new Block {Offsetx = 12, Length = 50},
                new Block {Offsetx = 0, Length = 2},
                new Block {Offsetx = 0, Length = 3},
                new Block {Offsetx = 1, Length = 3},
            });

            var merged = elem.MergeBlocks();

            merged.Collection.ShouldBeEquivalentTo(new Collection<Block>
            {
                new Block {Offsetx = 12, Length = 101},
                new Block {Offsetx = 12, Length = 55},
                new Block {Offsetx = 1, Length = 3},
            });
        }
    }
}