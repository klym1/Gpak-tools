using System.Linq;
using System.Threading;
using FluentAssertions;
using GPExtractor;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class BitIteratorTests
    {
        [Test]
        [TestCase((byte)0xff, new[] { 1, 1, 1, 1, 1, 1, 1, 1 })]
        [TestCase((byte)0x38, new[] { 0, 0, 1, 1, 1, 0, 0, 0 })]
        [TestCase((byte)0xf8, new[] { 1, 1, 1, 1, 1, 0, 0, 0 })]
        public void BitIteratorShouldWorkCorrectly(byte inputValue, int[] expectedBits)
        {
            var actualBits = Helper.IterateBits(inputValue).Select(it => it).ToList();

            actualBits.Should().Equal(expectedBits);
        }
    }
}