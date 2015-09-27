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
                new Block (12, 100),
                new Block (0, 1),
                new Block (12, 50),
                new Block (0, 2),
                new Block (0, 3),
                new Block (1, 3)
            }, 0);

            var merged = elem.MergeBlocks();

            merged.Collection.ShouldBeEquivalentTo(new Collection<Block>
            {
                new Block (12, 101),
                new Block (12, 55),
                new Block (1, 3)
            });
        }
    }
}