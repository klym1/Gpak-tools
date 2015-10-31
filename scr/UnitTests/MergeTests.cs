using System.Collections.Generic;
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
            var elem = new RawShapeBlocksGroup(new List<RawShapeBlock>
            {
                new RawShapeBlock (12, 100),
                new RawShapeBlock (0, 1),
                new RawShapeBlock (12, 50),
                new RawShapeBlock (0, 2),
                new RawShapeBlock (0, 3),
                new RawShapeBlock (1, 3)
            }, 0);

            var merged = elem.MergeBlocks();

            merged.Collection.ShouldBeEquivalentTo(new Collection<RawShapeBlock>
            {
                new RawShapeBlock (12, 101),
                new RawShapeBlock (12, 55),
                new RawShapeBlock (1, 3)
            });
        }
    }
}