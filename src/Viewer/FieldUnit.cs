using System;
using Avalonia;

namespace Viewer
{
    /// <summary>
    /// A unit on the field: where it stands, where it is walking to and which way it faces.
    /// It moves one step per animation frame, so its feet keep pace with the walk animation.
    /// </summary>
    public sealed class FieldUnit
    {
        /// <summary>Pixels walked per animation frame.</summary>
        public const double DefaultStepLength = 4;

        public FieldUnit(Point position, double heading = 0)
        {
            Position = position;
            Heading = heading;
            _facing = heading;
        }

        // The way the unit is turning to face.
        private double _facing;

        /// <summary>Degrees the unit turns per tick; null if it turns at once.</summary>
        public double? TurnRate { get; set; }

        /// <summary>Whether the unit has not yet turned the way it was told to face.</summary>
        public bool IsTurning => TurnRate != null && Heading != _facing;

        /// <summary>Turns a slow unit one tick further the shorter way round. Returns false if it already faces its way.</summary>
        public bool Turn()
        {
            if (!IsTurning)
            {
                return false;
            }

            var left = (_facing - Heading + 540) % 360 - 180;
            Heading = Math.Abs(left) <= TurnRate ? _facing : Normalized(Heading + Math.Sign(left) * TurnRate.Value);
            return true;
        }

        private void Face(double heading)
        {
            _facing = Normalized(heading);
            if (TurnRate == null)
            {
                Heading = _facing;
            }
        }

        private static double Normalized(double heading) => (heading % 360 + 360) % 360;

        /// <summary>Where the unit's feet (the sprite anchor) are, in field pixels.</summary>
        public Point Position { get; private set; }

        /// <summary>Where the unit is walking to, or null while it stands.</summary>
        public Point? Target { get; private set; }

        public bool IsWalking => Target != null;

        /// <summary>The way the unit faces, in degrees clockwise from south (the viewer), as directions are numbered.</summary>
        public double Heading { get; private set; }

        public double StepLength { get; set; } = DefaultStepLength;

        /// <summary>
        /// Sends the unit to a point: it turns towards it and starts walking. A point at (or very near) the
        /// unit's own position stops it where it is, facing the same way.
        /// </summary>
        public void MoveTo(Point target)
        {
            var way = target - Position;
            if (Length(way) < 0.5)
            {
                Target = null;
                return;
            }

            Face(HeadingOf(way));
            Target = target;
        }

        /// <summary>Turns the unit on the spot (degrees clockwise from south); a slow unit only starts to turn.</summary>
        public void TurnTo(double heading) => Face(heading);

        /// <summary>Stops walking where the unit is.</summary>
        public void Stop() => Target = null;

        /// <summary>Puts the unit somewhere else, standing.</summary>
        public void PlaceAt(Point position)
        {
            Position = position;
            Target = null;
        }

        /// <summary>Walks one step towards the target. Returns true on the step that arrives there.</summary>
        public bool Step()
        {
            if (Target is not { } target)
            {
                return false;
            }

            Position = NextPosition();
            if (Position != target)
            {
                return false;
            }

            Target = null;
            return true;
        }

        /// <summary>Where the next step takes the unit: towards the target, or onto it if it is near enough.</summary>
        public Point NextPosition()
        {
            if (Target is not { } target)
            {
                return Position;
            }

            var way = target - Position;
            var length = Length(way);
            return length <= StepLength ? target : Position + way * (StepLength / length);
        }

        /// <summary>
        /// The heading of a movement on screen (y pointing down), in degrees clockwise from south:
        /// down = 0, left = 90, up = 180, right = 270.
        /// </summary>
        public static double HeadingOf(Vector way)
        {
            return Normalized(Math.Atan2(-way.X, way.Y) * 180 / Math.PI);
        }

        private static double Length(Vector v) => Math.Sqrt(v.X * v.X + v.Y * v.Y);
    }
}
