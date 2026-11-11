using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using GPExtractor;

namespace Viewer
{
    /// <summary>How units get about: routes around each other, steps, and orders to go somewhere.</summary>
    public partial class MainWindow
    {
        /// <summary>Pixels a cell of a formation's picture is wide, until another is picked: the game's own is not known.</summary>
        public const int DefaultFormationSpacing = 24;

        /// <summary>The room a unit takes on the ground, as a radius: its <c>GEOMETRY</c>'s, or half its selection ring.</summary>
        private static double RadiusOf(FieldUnitView unit) => unit.Kind.Descriptor.Radius ?? RingWidthOf(unit) / 2;

        private static void Halt(FieldUnitView unit)
        {
            unit.Movement.Stop();
            unit.Route.Clear();
            unit.Goal = null;
        }

        /// <summary>
        /// Sends a unit to a point by a way around the other units, except those that
        /// <paramref name="passes"/> names (units about to move as well, or the enemy it goes for).
        /// </summary>
        private void WalkTo(FieldUnitView unit, Point goal, Func<FieldUnitView, bool> passes)
        {
            var obstacles = _fieldUnits
                .Where(other => !ReferenceEquals(other, unit) && !other.IsDead && !passes(other))
                .Select(other => new FieldObstacle(other.Movement.Position, RadiusOf(other) + RadiusOf(unit)))
                .ToList();

            unit.Route = new Queue<Point>(FieldPath.Find(
                  unit.Movement.Position
                , goal
                , obstacles
                , FieldSize()
            ));
            unit.Goal = goal;
            unit.WaitTicks = 0;

            if (NextLeg(unit))
            {
                SetFieldState(unit, WalkStateOf(unit.Kind));
            }
            else
            {
                Arrive(unit);
            }
        }

        /// <summary>The unit stands where its way ends, and in a formation turns to its front.</summary>
        private static void Arrive(FieldUnitView unit)
        {
            Halt(unit);
            if (unit.FacingAtGoal is { } facing && unit.Target == null)
            {
                unit.Movement.TurnTo(facing);
            }

            unit.FacingAtGoal = null;
            SetFieldState(unit, StandStateOf(unit.Kind));
        }

        /// <summary>Turns the unit to the next point of its route; false when there is none left.</summary>
        private static bool NextLeg(FieldUnitView unit)
        {
            while (unit.Route.Count > 0)
            {
                unit.Movement.MoveTo(unit.Route.Dequeue());
                if (unit.Movement.IsWalking)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The unit its next step would walk into, or null if the way is free. A unit already too near
        /// does not stop it from walking away.
        /// </summary>
        private FieldUnitView BlockerOf(FieldUnitView unit)
        {
            var here = unit.Movement.Position;
            var next = unit.Movement.NextPosition();

            return _fieldUnits.FirstOrDefault(other =>
            {
                if (ReferenceEquals(other, unit) || other.IsDead || ReferenceEquals(other, unit.Target))
                {
                    return false;
                }

                // Routes may cut a cell's width into the room of a unit they pass.
                var room = RadiusOf(unit) + RadiusOf(other) - FieldPath.CellSize;
                var distance = FieldPath.GroundDistance(next, other.Movement.Position);
                return distance < room && distance < FieldPath.GroundDistance(here, other.Movement.Position);
            });
        }

        /// <summary>
        /// A route ends beside the goal when a unit stood on it; if that unit has left since, the unit walks the
        /// rest straight. False if it is at its goal or the goal is still taken.
        /// </summary>
        private bool FinishAtGoal(FieldUnitView unit)
        {
            if (unit.Goal is not { } goal || Distance(goal, unit.Movement.Position) < 0.5)
            {
                return false;
            }

            var taken = _fieldUnits.Any(other =>
                !ReferenceEquals(other, unit) && !other.IsDead
                && FieldPath.GroundDistance(goal, other.Movement.Position) < RadiusOf(unit) + RadiusOf(other));
            if (taken)
            {
                return false;
            }

            unit.Movement.MoveTo(goal);
            return unit.Movement.IsWalking;
        }

        /// <summary>
        /// One step along the route. With a unit in the way it waits its turn behind one walking the same way,
        /// and otherwise finds a way round; it gives up when no way round helps.
        /// </summary>
        private void StepAlongRoute(FieldUnitView unit)
        {
            // A slow unit turns where it stands before it walks on.
            if (unit.Movement.Turn())
            {
                SetFieldState(unit, StandStateOf(unit.Kind));
                return;
            }

            SetFieldState(unit, WalkStateOf(unit.Kind));

            var blocker = BlockerOf(unit);
            if (blocker == null)
            {
                unit.WaitTicks = 0;
                unit.Detours = 0;
                if (unit.Movement.Step() && !NextLeg(unit) && !FinishAtGoal(unit))
                {
                    Arrive(unit);
                }

                return;
            }

            unit.WaitTicks++;
            var turn = Math.Abs((blocker.Movement.Heading - unit.Movement.Heading + 540) % 360 - 180);
            var follows = blocker.Movement.IsWalking && turn < 90;
            if (unit.WaitTicks < (follows ? MaxWaitTicks : 2))
            {
                return;
            }

            if (++unit.Detours > MaxDetours)
            {
                Halt(unit);
                SetFieldState(unit, StandStateOf(unit.Kind));
                return;
            }

            WalkTo(unit, unit.Goal ?? unit.Movement.Target ?? unit.Movement.Position, other => ReferenceEquals(other, unit.Target));
        }

        /// <summary>
        /// Sends the selected units to a point of the field: each turns towards its place in a block around the
        /// point, walks there and stands again. Units already walking keep their step.
        /// </summary>
        public void SendSelectedTo(Point target)
        {
            // Buildings stay where they are.
            var group = SelectedFieldUnits.Where(unit => !unit.Kind.IsBuilding).ToList();
            if (group.Count == 0)
            {
                return;
            }

            target = ClampToField(target);
            if (FormationBox.SelectedItem is GameFormation formation)
            {
                // The formation turns to look where it is sent; formed up where it stands, it looks as before.
                var way = target - Middle(group);
                var onGround = new Vector(way.X, way.Y / Formation.RowSpacing);
                if (onGround.Length > 1)
                {
                    _formationFacing = onGround / onGround.Length;
                }

                // Officers and drummers take the commanders' places, as far as there are any.
                var commanders = group.Where(unit => unit.Kind.Descriptor.IsCommander).Take(formation.Commanders.Count).ToList();
                var soldiers = group.Except(commanders).ToList();
                var places = Formation.Of(
                      formation
                    , target
                    , FormationSpacing
                    , soldiers.Count
                    , _formationFacing
                );
                var front = FieldUnit.HeadingOf(new Vector(_formationFacing.X, _formationFacing.Y * Formation.RowSpacing));
                SendToPlaces(
                      soldiers
                    , places.Soldiers
                    , group
                    , front
                );
                SendToPlaces(
                      commanders
                    , places.Commanders
                    , group
                    , front
                );
            }
            else
            {
                SendToPlaces(
                      group
                    , Formation.Around(target, group.Count, group.Max(SpacingOf))
                    , group
                    , null
                );
            }

            UpdateFieldView();
        }

        /// <summary>Puts the selected units into the picked formation where they are: around their middle.</summary>
        public void FormUpSelected()
        {
            var group = SelectedFieldUnits.Where(unit => !unit.Kind.IsBuilding).ToList();
            if (group.Count > 0)
            {
                SendSelectedTo(Middle(group));
            }
        }

        private static Point Middle(IReadOnlyList<FieldUnitView> units) => new Point(
              units.Average(unit => unit.Movement.Position.X)
            , units.Average(unit => unit.Movement.Position.Y)
        );

        // The way the formation looks, on the ground: up the screen until it is sent somewhere.
        private Vector _formationFacing = new Vector(0, -1);

        /// <summary>Pixels a cell of a formation's picture is wide.</summary>
        private double FormationSpacing => (double) (FormationSpacingBox.Value ?? DefaultFormationSpacing);

        private void SendToPlaces(
              IReadOnlyList<FieldUnitView> units
            , IReadOnlyList<Point> places
            , IReadOnlyList<FieldUnitView> group
            , double? facing
        )
        {
            var onField = places.Select(ClampToField).ToList();
            var assignment = Formation.Assign(units.Select(unit => unit.Movement.Position).ToList(), onField);

            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                unit.Target = null;
                unit.Detours = 0;
                unit.Next.Clear();
                unit.ReadyFor = null;
                unit.FacingAtGoal = facing;
                WalkTo(unit, onField[assignment[i]], group.Contains);
            }
        }

        private Size FieldSize()
        {
            var size = Field.Bounds.Size;
            return size.Width > 0 && size.Height > 0 ? size : ViewArea.Bounds.Size;
        }

        private Point ClampToField(Point point)
        {
            var size = FieldSize();
            return new Point(Math.Clamp(point.X, 0, Math.Max(size.Width, 0)), Math.Clamp(point.Y, 0, Math.Max(size.Height, 0)));
        }

        private Point FieldMiddle()
        {
            var size = FieldSize();
            return size.Width > 0 && size.Height > 0
                ? new Point(Math.Round(size.Width / 2), Math.Round(size.Height * 0.6))
                : new Point(240, 200);
        }

        /// <summary>The place nearest the middle of the field that is not taken by another unit.</summary>
        private Point FreePlace()
        {
            var size = FieldSize();
            var middle = FieldMiddle();
            var area = new Rect(size);
            return Formation.Around(middle, 121, 64)
                .Where(place => size.Width <= 0 || area.Contains(place))
                .OrderBy(place => Distance(place, middle))
                .Cast<Point?>()
                .FirstOrDefault(place => _fieldUnits.All(unit => Distance(unit.Movement.Position, place.Value) >= FreeSpace))
                ?? middle;
        }

        private static double Distance(Point a, Point b)
        {
            var way = a - b;
            return Math.Sqrt(way.X * way.X + way.Y * way.Y);
        }
    }
}
