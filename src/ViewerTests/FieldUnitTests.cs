using Avalonia;
using NUnit.Framework;
using Viewer;

namespace ViewerTests
{
    public class FieldUnitTests
    {
        [TestCase(0, 1, 0, TestName = "Down is south")]
        [TestCase(-1, 0, 90, TestName = "Left is west")]
        [TestCase(0, -1, 180, TestName = "Up is north")]
        [TestCase(1, 0, 270, TestName = "Right is east")]
        [TestCase(-1, 1, 45, TestName = "Down-left is south-west")]
        [TestCase(1, -1, 225, TestName = "Up-right is north-east")]
        public void HeadingIsClockwiseFromSouthLikeDirections(double dx, double dy, double heading)
        {
            Assert.That(FieldUnit.HeadingOf(new Vector(dx, dy)), Is.EqualTo(heading).Within(1e-9));
        }

        [Test]
        public void WalksOneStepPerFrameAndStopsAtTheTarget()
        {
            var unit = new FieldUnit(new Point(100, 100));
            Assert.That(unit.IsWalking, Is.False);

            unit.MoveTo(new Point(100, 90));
            Assert.That(unit.IsWalking, Is.True);
            Assert.That(unit.Heading, Is.EqualTo(180), "faces north");

            Assert.That(unit.Step(), Is.False);
            Assert.That(unit.Position, Is.EqualTo(new Point(100, 96)));
            Assert.That(unit.Step(), Is.False);
            Assert.That(unit.Step(), Is.True, "arrives on the step that reaches the target");
            Assert.That(unit.Position, Is.EqualTo(new Point(100, 90)));
            Assert.That(unit.IsWalking, Is.False);
            Assert.That(unit.Heading, Is.EqualTo(180), "keeps facing the way it walked");
            Assert.That(unit.Step(), Is.False, "a standing unit stays put");
        }

        [Test]
        public void ClickingOnItselfStopsTheUnitWithoutTurning()
        {
            var unit = new FieldUnit(new Point(50, 50));
            unit.MoveTo(new Point(80, 50));
            unit.Step();

            unit.MoveTo(unit.Position);

            Assert.That(unit.IsWalking, Is.False);
            Assert.That(unit.Heading, Is.EqualTo(270));
        }
    }
}
