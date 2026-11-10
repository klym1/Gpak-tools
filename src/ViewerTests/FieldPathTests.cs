using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using NUnit.Framework;
using Viewer;

namespace ViewerTests
{
    public class FieldPathTests
    {
        private static readonly Size Field = new Size(800, 400);

        /// <summary>The nearest the route (walked in small steps) comes to a point, on the ground.</summary>
        private static double NearestApproach(Point from, IReadOnlyList<Point> route, Point to)
        {
            var nearest = double.MaxValue;
            var at = from;
            foreach (var leg in route)
            {
                for (var i = 0; i <= 100; i++)
                {
                    nearest = Math.Min(nearest, FieldPath.GroundDistance(at + (leg - at) * (i / 100.0), to));
                }

                at = leg;
            }

            return nearest;
        }

        [Test]
        public void GoesStraightWhenNothingIsInTheWay()
        {
            var aside = new[] { new FieldObstacle(new Point(300, 300), 30) };

            Assert.That(FieldPath.Find(new Point(100, 200), new Point(500, 200), Array.Empty<FieldObstacle>(), Field), Is.EqualTo(new[] { new Point(500, 200) }));
            Assert.That(FieldPath.Find(new Point(100, 200), new Point(500, 200), aside, Field), Is.EqualTo(new[] { new Point(500, 200) }));
        }

        [Test]
        public void GoesRoundAnObstacleOnTheRight()
        {
            var from = new Point(100, 200);
            var to = new Point(500, 200);
            var obstacle = new FieldObstacle(new Point(300, 200), 40);

            var route = FieldPath.Find(from, to, new[] { obstacle }, Field);

            Assert.That(route.Last(), Is.EqualTo(to));
            Assert.That(route.Count, Is.InRange(2, 6), "a few straight legs");
            Assert.That(NearestApproach(from, route, obstacle.Centre), Is.GreaterThanOrEqualTo(obstacle.Radius - FieldPath.CellSize));

            // Walking east, its right is down the screen; the way back passes on the other side.
            Assert.That(route.Take(route.Count - 1).Select(point => point.Y), Is.All.GreaterThan(200));
            var back = FieldPath.Find(to, from, new[] { obstacle }, Field);
            Assert.That(back.Take(back.Count - 1).Select(point => point.Y), Is.All.LessThan(200));
        }

        [Test]
        public void EndsBesideAnObstacleThatIsOnTheGoal()
        {
            var obstacle = new FieldObstacle(new Point(500, 200), 30);

            var route = FieldPath.Find(new Point(100, 200), obstacle.Centre, new[] { obstacle }, Field);

            var distance = FieldPath.GroundDistance(route.Last(), obstacle.Centre);
            Assert.That(distance, Is.InRange(obstacle.Radius - FieldPath.CellSize, obstacle.Radius + 2 * FieldPath.CellSize));
        }

        [Test]
        public void LeavesAPlaceInsideAnObstacle()
        {
            var obstacle = new FieldObstacle(new Point(110, 200), 30);

            var route = FieldPath.Find(new Point(100, 200), new Point(500, 200), new[] { obstacle }, Field);

            Assert.That(route.Last(), Is.EqualTo(new Point(500, 200)));
        }
    }
}
