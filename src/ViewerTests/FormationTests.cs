using System.Linq;
using Avalonia;
using NUnit.Framework;
using Viewer;

namespace ViewerTests
{
    public class FormationTests
    {
        [Test]
        public void OneUnitGoesExactlyToThePoint()
        {
            Assert.That(Formation.Around(new Point(10, 20), 1, 30), Is.EqualTo(new[] { new Point(10, 20) }));
        }

        [Test]
        public void PlacesFormABlockCentredOnThePoint()
        {
            var places = Formation.Around(new Point(100, 100), 5, 20);

            // 3 columns: a row of 3, then a centred row of 2.
            var row = 20 * Formation.RowSpacing;
            Assert.That(places, Is.EqualTo(new[]
            {
                new Point(80, 100 - row / 2), new Point(100, 100 - row / 2), new Point(120, 100 - row / 2),
                new Point(90, 100 + row / 2), new Point(110, 100 + row / 2),
            }));
        }

        [Test]
        public void UnitsTakeTheNearestPlacesWithoutSharing()
        {
            var units = new[] { new Point(0, 0), new Point(100, 0), new Point(50, 50) };
            var places = new[] { new Point(110, 10), new Point(45, 55), new Point(-10, 0) };

            Assert.That(Formation.Assign(units, places), Is.EqualTo(new[] { 2, 0, 1 }));
        }

        [Test]
        public void UnitsOnTopOfEachOtherStillGetTheirOwnPlace()
        {
            var units = Enumerable.Repeat(new Point(5, 5), 4).ToArray();
            var assignment = Formation.Assign(units, Formation.Around(new Point(5, 5), 4, 10));

            Assert.That(assignment.OrderBy(i => i), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }
    }
}
