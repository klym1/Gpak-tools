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
    /// <summary>Shots: what a weapon shows when fired, the flight, and the damage where it lands.</summary>
    public partial class MainWindow
    {
        private sealed record ShotInFlight(int TicksLeft, FieldUnitView Attacker, string Attack, Point Where, double BlastRadius);

        /// <summary>
        /// A shot lands at a point: it hits the units within its blast, whoever they are, but not the one that
        /// fired it. A unit it lands on takes all the damage; in a blast the damage falls off evenly to nothing
        /// at the edge.
        /// </summary>
        private void Land(FieldUnitView attacker, string attack, Point where, double blastRadius)
        {
            var reach = Math.Max(blastRadius, HitSlack);
            var struck = _fieldUnits
                .Where(unit => !ReferenceEquals(unit, attacker) && !unit.IsDead)
                .Select(unit => (Unit: unit, Away: Math.Max(0, FieldPath.GroundDistance(unit.Movement.Position, where) - RadiusOf(unit))))
                .Where(hit => hit.Away <= reach)
                .ToList();

            foreach (var (unit, away) in struck)
            {
                Hit(
                      attacker.Kind
                    , attack
                    , unit
                    , blastRadius > HitSlack ? 1 - away / blastRadius : 1
                );
            }
        }

        /// <summary>Lands the shots whose time has come.</summary>
        private void LandShots()
        {
            for (var i = _shotsInFlight.Count - 1; i >= 0; i--)
            {
                var shot = _shotsInFlight[i];
                if (shot.TicksLeft > 1)
                {
                    _shotsInFlight[i] = shot with { TicksLeft = shot.TicksLeft - 1 };
                    continue;
                }

                _shotsInFlight.RemoveAt(i);
                Land(
                      shot.Attacker
                    , shot.Attack
                    , shot.Where
                    , shot.BlastRadius
                );
            }
        }

        private IReadOnlyList<GameObjectShot> ShotsOf(GameObject kind, GameObjectState state)
        {
            var key = (kind, state);
            if (!_stateShots.TryGetValue(key, out var shots))
            {
                shots = _catalog.ShotsOf(kind, state);
                _stateShots[key] = shots;
            }

            return shots;
        }

        /// <summary>
        /// Shows what the unit's state fires at the frame it is in: smoke at the weapon, and a while later
        /// what the shot does where it lands: on the unit's enemy, or as far ahead as a shot reaches.
        /// </summary>
        private void Fire(FieldUnitView unit)
        {
            if (unit.State.Frames.Count == 0)
            {
                return;
            }

            var shots = ShotsOf(unit.Kind, unit.State);
            var frame = unit.Pose % unit.State.Frames.Count;
            foreach (var shot in shots.Where(shot => shot.Frame == frame))
            {
                var (muzzle, impact, flying) = _catalog.Weapons.EffectsOf(shot.Weapon);
                var from = WeaponPositionOf(unit, shot, frame);
                foreach (var effect in muzzle)
                {
                    AddEffect(
                          effect.Animation
                        , from
                        , unit.Movement.Heading
                        , effect.Transparency
                    );
                }

                // What flies or bursts takes its time to get there; a bullet is there at once.
                var takesTime = flying.Count > 0 || impact.Count > 0;
                var heading = unit.Movement.Heading * Math.PI / 180;
                var ahead = new Vector(-Math.Sin(heading), Math.Cos(heading) / FieldPath.Slant);
                var reach = ShotReachOf(unit, unit.State.Code) / FieldPath.GroundDistance(default, new Point(ahead.X, ahead.Y));
                // How long the shot is in the air: as its weapon flies over the distance, as near as can be told
                // before aiming ahead of a walking target.
                var straight = unit.Target?.Movement.Position ?? ClampToField(unit.Movement.Position + ahead * reach);
                var ticks = flying.Count > 0 ? flying[0].TicksOver(Distance(from, straight)) : TicksToImpact;
                unit.ShotTicks = ticks;
                var aim = unit.Target != null ? AimAt(unit.Target, takesTime ? ticks : 0) : straight;
                var where = Scattered(unit, aim);

                // A shot that lands on its target hits it as high as the target is (ZPOINTS), not at its feet.
                var onTarget = unit.Target != null && FieldPath.GroundDistance(where, aim) <= RadiusOf(unit.Target) + HitSlack;
                var end = where - new Vector(0, onTarget ? unit.Target.Kind.Descriptor.Height ?? 0 : 0);

                foreach (var flight in flying)
                {
                    // The top of a throw is a quarter of its length times the tangent of the launch angle.
                    var height = Distance(from, where) * Math.Tan(flight.LaunchAngle * Math.PI / 180) / 4;
                    var projectile = AddEffect(flight.Animation, from, FieldUnit.HeadingOf(where - from));
                    if (projectile != null)
                    {
                        projectile.Flight = (from, end, height, ticks);
                        projectile.Shadow = new Ellipse
                        {
                            Width = 6,
                            Height = 3,
                            Fill = Brushes.Black,
                            Opacity = 0.35,
                            IsHitTestVisible = false,
                            ZIndex = 2,
                        };
                        Field.Children.Add(projectile.Shadow);
                    }
                }

                if (impact.Count > 0)
                {
                    _impactEffects.Add((ticks, impact[_random.Next(impact.Count)], where));
                }

                if (takesTime)
                {
                    _shotsInFlight.Add(new ShotInFlight(
                          ticks
                        , unit
                        , unit.State.Code
                        , where
                        , flying.Select(flight => flight.BlastRadius).DefaultIfEmpty(0).Max()
                    ));
                }
                else
                {
                    Land(
                          unit
                        , unit.State.Code
                        , where
                        , 0
                    );
                }
            }
        }

        /// <summary>
        /// Where a shot aimed at a point lands: anywhere within the unit's scatter (<c>RAZBROS</c>) of it, which
        /// grows with the distance.
        /// </summary>
        private Point Scattered(FieldUnitView unit, Point aim)
        {
            if (unit.Kind.Descriptor.Scatter is not { } scatter)
            {
                return aim;
            }

            var radius = FieldPath.GroundDistance(unit.Movement.Position, aim) * scatter / ScatterDistance;
            var away = radius * Math.Sqrt(_random.NextDouble());
            var angle = _random.NextDouble() * 2 * Math.PI;
            return ClampToField(aim + new Vector(Math.Cos(angle) * away, Math.Sin(angle) * away / FieldPath.Slant));
        }

        /// <summary>
        /// Where to shoot to hit a unit with a shot that takes some ticks to land: where the unit will be by
        /// then if it keeps walking the way it does.
        /// </summary>
        private static Point AimAt(FieldUnitView target, int ticks)
        {
            var position = target.Movement.Position;
            if (ticks == 0 || target.Movement.Target is not { } walkingTo)
            {
                return position;
            }

            var way = walkingTo - position;
            var length = Math.Sqrt(way.X * way.X + way.Y * way.Y);
            var walked = Math.Min(length, target.Movement.StepLength * ticks);
            return length < 0.5 ? position : position + way * (walked / length);
        }

        /// <summary>
        /// Where the unit's weapon is on the field: the shot's point for the direction the unit faces, taken
        /// from the anchor of the sprite it is drawn from, and mirrored like the sprite.
        /// </summary>
        private static Point WeaponPositionOf(FieldUnitView unit, GameObjectShot shot, int frame)
        {
            var sprite = unit.Kind.Sprite(unit.State.Frames[frame].Sprite);
            if (shot.Points.Count == 0 || sprite?.AnchorX == null)
            {
                return unit.Movement.Position;
            }

            var directions = unit.Directions;
            var mirrored = directions != null && unit.Direction >= directions.StoredDirections;
            var stored = mirrored ? directions.Count - unit.Direction : unit.Direction;
            var point = shot.Points[Math.Min(stored, shot.Points.Count - 1)];

            var aside = point.X - sprite.AnchorX.Value;
            return unit.Movement.Position + new Vector(mirrored ? -aside : aside, point.Y - (sprite.AnchorY ?? 0));
        }

        /// <summary>Puts a weapon animation on the field, to play once; null if there is no such animation.</summary>
        private FieldUnitView AddEffect(string animation, Point position, double heading, string transparency = null)
        {
            var kind = _catalog.Effect(animation);
            if (kind == null)
            {
                return null;
            }

            // The last row of the game's blend table is the one color everything turns into under the thickest smoke.
            var blend = _catalog.BlendTable(transparency);

            var effect = new FieldUnitView(
                  kind
                , NationColorOffset.Red
                , kind.States[0]
                , new FieldUnit(position, heading)
            )
            {
                Fog = blend == null ? null : _palette[blend[blend.Rows - 1, 0]],
            };
            Field.Children.Add(effect.Image);
            _effects.Add(effect);
            return effect;
        }

        /// <summary>Plays the effects on, removes those that are over, and shows the impacts whose time has come.</summary>
        private void AdvanceEffects()
        {
            foreach (var effect in _effects.ToList())
            {
                effect.Pose++;

                // A projectile flies its arc, its animation looping, and is gone when it lands.
                var over = effect.Pose >= effect.State.Frames.Count;
                if (effect.Flight is { } flight)
                {
                    var done = ++effect.FlightAge / (double) flight.Ticks;
                    var onGround = flight.From + (flight.To - flight.From) * done;
                    var before = effect.Movement.Position;
                    effect.Movement.PlaceAt(onGround + new Vector(0, -4 * flight.Height * done * (1 - done)));
                    over = effect.FlightAge >= flight.Ticks;

                    // It points the way it moves on the screen, so an arrow tips over at the top of its arc.
                    if (effect.Movement.Position != before)
                    {
                        effect.Movement.TurnTo(FieldUnit.HeadingOf(effect.Movement.Position - before));
                    }

                    // Its shadow runs along the ground, under where it would be without the arc.
                    var foot = onGround;
                    Canvas.SetLeft(effect.Shadow, foot.X - effect.Shadow.Width / 2);
                    Canvas.SetTop(effect.Shadow, foot.Y - effect.Shadow.Height / 2);
                }

                if (over)
                {
                    Field.Children.Remove(effect.Image);
                    Field.Children.Remove(effect.Shadow);
                    _effects.Remove(effect);
                }
            }

            for (var i = _impactEffects.Count - 1; i >= 0; i--)
            {
                var (ticksLeft, effect, where) = _impactEffects[i];
                if (ticksLeft > 1)
                {
                    _impactEffects[i] = (ticksLeft - 1, effect, where);
                    continue;
                }

                _impactEffects.RemoveAt(i);
                AddEffect(
                      effect.Animation
                    , where
                    , 0
                    , effect.Transparency
                );
            }
        }
    }
}
