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
    /// <summary>
    /// The field: units of the game data placed side by side. Click a unit or drag a box to select units,
    /// right-click to send the selection somewhere: each turns, walks to its place in a block around the
    /// point and stands again.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>How far the pointer moves before a press on empty field becomes a selection box.</summary>
        private const double DragThreshold = 4;

        /// <summary>Nearest a new unit is put to another one.</summary>
        private const double FreeSpace = 50;

        // The armies the field starts with: these kinds (by descriptor name; PUS is the cannon), one of each a side.
        private static readonly string[] StartingUnits = { "PUS", "MUS", "GRE", "PIR", "DR2" };
        private static readonly NationColorOffset EnemyColor = NationColorOffset.Red;
        private const double ArmyDistance = 300;
        private const double ArmyRowSpace = 70;

        private static readonly IBrush RingBrush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(0x9C, 0xFF, 0x57));

        private readonly List<FieldUnitView> _fieldUnits = new List<FieldUnitView>();

        // Frames of a unit kind's state from one direction in one color: ready, or being rendered.
        private readonly Dictionary<FieldFramesKey, FieldFrames> _readyFrames = new Dictionary<FieldFramesKey, FieldFrames>();
        private readonly Dictionary<FieldFramesKey, Task> _renderingFrames = new Dictionary<FieldFramesKey, Task>();

        // Sounds per unit kind and state.
        private readonly Dictionary<(GameObject, GameObjectState), IReadOnlyList<(int Frame, GameSound Sound, bool Impact)>> _stateSounds = new();

        // The unit kind whose attacks the attack list shows.
        private GameObject _attackBoxKind;

        /// <summary>
        /// How near, in pixels, a unit comes to an enemy to hit it by hand, and to fire at it if its descriptor
        /// gives no <c>ATTACK_RADIUS</c>.
        /// </summary>
        private const double MeleeReach = 40;
        private const double ShotReach = 300;

        private const double LifeBarWidth = 30;

        /// <summary>The <c>GEOMETRY</c> step of a foot soldier, who walks <see cref="FieldUnit.DefaultStepLength"/> a frame.</summary>
        private const double FootStepSize = 32;

        /// <summary><c>ROTATE</c> is taken as parts of a turn per tick, a full turn being this many.</summary>
        private const double TurnParts = 256;

        /// <summary><c>RAZBROS</c> is taken as how far, in pixels, a shot at this distance may land from its aim.</summary>
        private const double ScatterDistance = 2048;

        /// <summary>How far, in pixels, an enemy moves before the way to it is planned again.</summary>
        private const double RetargetDistance = 12;

        /// <summary>Ticks a unit waits behind one walking its way, and ways round it tries, before something else is done.</summary>
        private const int MaxWaitTicks = 25;
        private const int MaxDetours = 6;

        /// <summary>How long after the shot a weapon lands, in ticks.</summary>
        private const int TicksToImpact = 20;

        private readonly List<(int TicksLeft, GameSound Sound)> _impacts = new();

        // Weapon animations playing on the field (smoke, explosions), and those still to appear where a shot lands.
        private readonly List<FieldUnitView> _effects = new();
        private readonly List<(int TicksLeft, WeaponEffect Effect, Point Where)> _impactEffects = new();
        private readonly Dictionary<(GameObject, GameObjectState), IReadOnlyList<GameObjectShot>> _stateShots = new();

        // Shots on their way: they do their damage where they land, to whoever is there then.
        private readonly List<ShotInFlight> _shotsInFlight = new();

        private sealed record ShotInFlight(int TicksLeft, FieldUnitView Attacker, string Attack, Point Where, double BlastRadius);

        /// <summary>How far, in pixels on the ground, a shot without a blast may land from a unit's footprint and still hit it.</summary>
        private const double HitSlack = 6;

        /// <summary>The weapon animations playing on the field now.</summary>
        public IReadOnlyList<FieldUnitView> Effects => _effects;
        private readonly Random _random = new Random();

        /// <summary>What plays the units' sounds.</summary>
        public ISoundPlayer SoundPlayer { get; set; } = new SoundPlayer();

        /// <summary>How long a killed unit lies on the field, in ticks.</summary>
        private const int TicksLyingDead = 100;

        // A selection box being dragged.
        private Point? _dragStart;
        private bool _dragging;
        private bool _dragAdds;

        private readonly record struct FieldFramesKey(GameObject Kind, GameObjectState State, int Direction, int Color, int Fog);

        /// <summary>Whether game data is open, so units can be put on the field.</summary>
        private bool IsFieldMode => _catalog != null;

        /// <summary>Units on the field, in the order they were added.</summary>
        public IReadOnlyList<FieldUnitView> FieldUnits => _fieldUnits;

        public IReadOnlyList<FieldUnitView> SelectedFieldUnits => _fieldUnits.Where(unit => unit.IsSelected).ToList();

        /// <summary>
        /// Adds a unit to the field by descriptor name (e.g. <c>DR2</c>), as picking it in the unit list does:
        /// in the picked color, standing and facing the viewer, at <paramref name="at"/> or in a free place
        /// near the middle; it becomes the selection.
        /// </summary>
        public FieldUnitView AddToField(string descriptorName, Point? at = null)
        {
            var kind = FindUnit(_catalog, descriptorName)
                       ?? throw new ArgumentException("No such unit: " + descriptorName, nameof(descriptorName));
            return AddToField(kind, at);
        }

        private static GameObject FindUnit(GameCatalog catalog, string descriptorName) =>
            catalog?.Units.FirstOrDefault(o => o.Descriptor.FileName.Equals(descriptorName, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Puts two armies on the field, either side of the middle: one unit of each kind in the picked color
        /// on the left, which become the selection, and one in <see cref="EnemyColor"/> on the right.
        /// </summary>
        private void AddStartingArmies(IReadOnlyList<GameObject> kinds)
        {
            var middle = FieldMiddle();
            var own = new List<FieldUnitView>();
            for (var row = 0; row < kinds.Count; row++)
            {
                var y = middle.Y + (row - (kinds.Count - 1) / 2.0) * ArmyRowSpace;
                own.Add(AddToField(kinds[row], ClampToField(new Point(middle.X - ArmyDistance / 2, y))));
                AddToField(
                      kinds[row]
                    , ClampToField(new Point(middle.X + ArmyDistance / 2, y))
                    , EnemyColor
                );
            }

            Select(own, add: false);
        }

        private FieldUnitView AddToField(GameObject kind, Point? at = null, NationColorOffset? color = null)
        {
            var descriptor = kind.Descriptor;
            var movement = new FieldUnit(at ?? FreePlace())
            {
                StepLength = FieldUnit.DefaultStepLength * (descriptor.StepSize is > 0 and var step ? step : FootStepSize) / FootStepSize,
                TurnRate = descriptor.TurnRate * 360 / TurnParts,
            };

            var unit = new FieldUnitView(kind, color ?? PlayerColor, StandStateOf(kind), movement)
            {
                Ring = new Ellipse { Stroke = RingBrush, StrokeThickness = 2, IsHitTestVisible = false, IsVisible = false },
                Marker = new Ellipse { Width = 14, Height = 7, Stroke = Brushes.White, StrokeThickness = 1.5, IsHitTestVisible = false, IsVisible = false },
                LifeBar = Bar(3, RingBrush),
                ReloadBar = Bar(2, Brushes.White),
            };

            Field.Children.Add(unit.LifeBar);
            Field.Children.Add(unit.ReloadBar);

            Field.Children.Add(unit.Ring);
            Field.Children.Add(unit.Marker);
            Field.Children.Add(unit.Image);
            _fieldUnits.Add(unit);

            Select(new[] { unit }, add: false);
            return unit;
        }

        private static Rectangle Bar(double height, IBrush fill) =>
            new Rectangle { Height = height, Fill = fill, IsHitTestVisible = false, IsVisible = false, ZIndex = 99999 };

        /// <summary>Removes the selected units from the field.</summary>
        public void RemoveSelectedFromField()
        {
            foreach (var unit in SelectedFieldUnits)
            {
                RemoveFromField(unit);
            }

            UpdateFieldView();
        }

        /// <summary>
        /// The selected units stop and play an attack once, with its sounds, then stand again: the attack of
        /// that code (e.g. <c>ATTACK2</c>) or picked in the attack list, or else each unit's own first choice.
        /// </summary>
        public void AttackWithSelected(string code = null)
        {
            code ??= (AttackBox.SelectedItem as AttackOption)?.State.Code;

            foreach (var unit in SelectedFieldUnits)
            {
                var attack = AttacksOf(unit.Kind).FirstOrDefault(state => state.Code == code) ?? AttackStateOf(unit.Kind);
                if (attack == null)
                {
                    continue;
                }

                unit.Target = null;
                Halt(unit);
                PlayOnce(unit, attack);
            }

            UpdateFieldView();
        }

        /// <summary>
        /// The selected units die, with their death sound: they play their death once, lie dead for a while and
        /// leave the field.
        /// </summary>
        public void KillSelected()
        {
            foreach (var unit in SelectedFieldUnits)
            {
                Kill(unit);
            }

            UpdateFieldView();
        }

        /// <summary>
        /// Sends the selected units of another color against a unit: each comes within reach of its attack
        /// (the one picked in the attack list, if it has it), turns to the enemy and attacks, which kills it.
        /// </summary>
        public void AttackUnit(FieldUnitView target)
        {
            foreach (var unit in SelectedFieldUnits.Where(unit => IsEnemy(unit, target)))
            {
                unit.Target = target;
                unit.TargetIsOrdered = true;
            }

            UpdateFieldView();
        }

        /// <summary>
        /// Tells the selected units that can (<c>STANDGROUND</c>) to hold their place, or to stop holding it: a
        /// unit holding its place attacks the enemies that come within reach, but goes after one only when sent.
        /// </summary>
        public void StandGroundWithSelected(bool hold)
        {
            foreach (var unit in SelectedFieldUnits.Where(unit => unit.Kind.Descriptor.CanStandGround))
            {
                unit.StandsGround = hold;
            }

            UpdateFieldView();
        }

        private static bool IsEnemy(FieldUnitView unit, FieldUnitView other) =>
            !ReferenceEquals(unit, other) && unit.Color.Offset != other.Color.Offset;

        private void Kill(FieldUnitView unit)
        {
            Halt(unit);
            unit.Target = null;
            unit.IsDead = true;
            unit.IsSelected = false;

            var death = StateOf(unit.Kind, "DEATH");
            if (death != null)
            {
                PlayOnce(unit, death);
            }
        }

        /// <summary>The attack a unit uses on an enemy: the one picked in the attack list, or its first choice.</summary>
        private GameObjectState ChosenAttackOf(GameObject kind)
        {
            var code = (AttackBox.SelectedItem as AttackOption)?.State.Code;
            return AttacksOf(kind).FirstOrDefault(state => state.Code == code) ?? AttackStateOf(kind);
        }

        /// <summary>
        /// Whether a unit is near enough to hit with an attack: one that fires a weapon reaches as far as its
        /// <c>ATTACK_RADIUS</c>, measured on the ground.
        /// </summary>
        private static bool IsWithinReach(FieldUnitView unit, FieldUnitView target, GameObjectState attack)
        {
            var from = unit.Movement.Position;
            var to = target.Movement.Position;

            // Large units cannot come as near as small ones.
            var room = RadiusOf(unit) + RadiusOf(target) + 4;
            return Fires(unit.Kind, attack)
                ? FieldPath.GroundDistance(from, to) <= Math.Max(ShotReachOf(unit, attack.Code), room)
                : Distance(from, to) <= Math.Max(MeleeReach, room);
        }

        private static bool Fires(GameObject kind, GameObjectState attack) =>
            kind.Descriptor.Weapons.Any(weapon => GameCatalog.SameAnimation(weapon.Animation, attack.Code));

        /// <summary>Whether a target is nearer than an attack that fires a weapon can be made: the minimum of its <c>ATTACK_RADIUS</c>.</summary>
        private static bool IsTooNear(FieldUnitView unit, FieldUnitView target, GameObjectState attack) =>
            Fires(unit.Kind, attack)
            && FieldPath.GroundDistance(unit.Movement.Position, target.Movement.Position) < (unit.Kind.Descriptor.AttackMinRangeOf(attack.Code) ?? 0);

        /// <summary>Whether a target is at a distance at which the unit chooses an attack: within its <c>DET_RADIUS</c>, if it has one.</summary>
        private static bool IsChosenAt(FieldUnitView unit, FieldUnitView target, GameObjectState attack)
        {
            var away = FieldPath.GroundDistance(unit.Movement.Position, target.Movement.Position);
            return unit.Kind.Descriptor.DetectionRadiusOf(attack.Code) is not { } band || (away >= band.Min && away <= band.Max);
        }

        /// <summary>
        /// The attack a unit makes on a target, of those the target is not too near for: the one it chooses at
        /// that distance, the picked one first, or else just the picked one or the first of the others. Null if
        /// the target is too near for all of them.
        /// </summary>
        private static GameObjectState AttackOn(FieldUnitView unit, FieldUnitView target, GameObjectState chosen)
        {
            var attacks = AttacksOf(unit.Kind).Prepend(chosen).Where(attack => !IsTooNear(unit, target, attack)).ToList();
            return attacks.FirstOrDefault(attack => IsChosenAt(unit, target, attack)) ?? attacks.FirstOrDefault();
        }

        /// <summary>Whether a unit can attack a target from where it stands.</summary>
        private bool CanReach(FieldUnitView unit, FieldUnitView target) =>
            ChosenAttackOf(unit.Kind) is { } chosen
            && AttackOn(unit, target, chosen) is { } attack
            && IsWithinReach(unit, target, attack);

        private static double ShotReachOf(FieldUnitView unit, string attack) =>
            unit.Kind.Descriptor.AttackRangeOf(attack) ?? ShotReach;

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
                Halt(unit);
                SetFieldState(unit, StandStateOf(unit.Kind));
            }
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
                    Halt(unit);
                    SetFieldState(unit, StandStateOf(unit.Kind));
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
        /// An idle unit that is attacked turns on its attacker, if it can attack at all, and if it need not
        /// leave a place it holds.
        /// </summary>
        private void FightBack(FieldUnitView unit, FieldUnitView attacker)
        {
            if (CanTakeOn(unit, attacker))
            {
                TakeOn(unit, attacker);
            }
        }

        /// <summary>An idle unit takes on the nearest enemy within its <c>SEARCH_ENEMY_RADIUS</c>.</summary>
        private void LookForEnemy(FieldUnitView unit)
        {
            if (unit.Kind.Descriptor.SearchEnemyRadius is not { } radius)
            {
                return;
            }

            var enemy = _fieldUnits
                .Where(other => IsEnemy(unit, other) && !other.IsDead && CanTakeOn(unit, other))
                .Select(other => (Unit: other, Away: FieldPath.GroundDistance(unit.Movement.Position, other.Movement.Position)))
                .Where(found => found.Away <= radius)
                .OrderBy(found => found.Away)
                .Select(found => found.Unit)
                .FirstOrDefault();
            if (enemy != null)
            {
                TakeOn(unit, enemy);
            }
        }

        private bool CanTakeOn(FieldUnitView unit, FieldUnitView enemy)
        {
            var idle = unit.Target == null && !unit.IsDead && !unit.PlaysOnce && !unit.Movement.IsWalking;
            return idle && AttackStateOf(unit.Kind) != null && (!unit.StandsGround || CanReach(unit, enemy));
        }

        private static void TakeOn(FieldUnitView unit, FieldUnitView enemy)
        {
            unit.Target = enemy;
            unit.TargetIsOrdered = false;
        }

        /// <summary>
        /// An attack lands on a unit: it loses the attack's damage (or the share of it that reaches the unit),
        /// less its armor, from its life (all of it, if the attacker has no damage given) and dies when none is left.
        /// </summary>
        private void Hit(GameObject attacker, string attack, FieldUnitView target, double share = 1)
        {
            var damage = attacker.Descriptor.DamageTo(target.Kind.Descriptor, attack, share) ?? target.Life;
            target.Life = Math.Max(0, target.Life - damage);
            if (target.Life == 0)
            {
                Kill(target);
            }
        }

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

        /// <summary>
        /// Goes after the unit's target: walks until it is within reach, then turns to it and attacks.
        /// </summary>
        private void Pursue(FieldUnitView unit, bool resting)
        {
            var target = unit.Target;
            var attack = ChosenAttackOf(unit.Kind);
            if (target.IsDead || !_fieldUnits.Contains(target) || attack == null)
            {
                unit.Target = null;
                Halt(unit);
                SetFieldState(unit, StandStateOf(unit.Kind));
                return;
            }

            if (unit.PlaysOnce)
            {
                return;
            }

            var way = target.Movement.Position - unit.Movement.Position;
            attack = AttackOn(unit, target, attack);
            if (attack == null || IsWithinReach(unit, target, attack))
            {
                Halt(unit);
                unit.Movement.TurnTo(FieldUnit.HeadingOf(way));
                var turning = unit.Movement.Turn();
                if (attack == null || resting || turning)
                {
                    SetFieldState(unit, StandStateOf(unit.Kind));
                    return;
                }

                PlayOnce(unit, attack);
                FightBack(target, unit);
            }
            else if (unit.StandsGround && !unit.TargetIsOrdered)
            {
                unit.Target = null;
                Halt(unit);
                SetFieldState(unit, StandStateOf(unit.Kind));
            }
            else
            {
                // Plan the way again when the enemy has moved, or the unit was stopped on its way.
                var goal = target.Movement.Position;
                if (unit.Goal is not { } planned || Distance(planned, goal) > RetargetDistance || !unit.Movement.IsWalking)
                {
                    WalkTo(unit, goal, other => ReferenceEquals(other, target));
                }
            }
        }

        internal void RemoveFromField(FieldUnitView unit)
        {
            Field.Children.Remove(unit.Image);
            Field.Children.Remove(unit.Ring);
            Field.Children.Remove(unit.Marker);
            Field.Children.Remove(unit.LifeBar);
            Field.Children.Remove(unit.ReloadBar);
            _fieldUnits.Remove(unit);
        }

        private void PlayOnce(FieldUnitView unit, GameObjectState state)
        {
            unit.State = state;
            unit.Pose = 0;
            unit.PlaysOnce = true;
            PlaySounds(unit);
            Fire(unit);
        }

        /// <summary>The unit's next frame; false when it has lain dead long enough and leaves the field.</summary>
        private bool Advance(FieldUnitView unit)
        {
            if (unit.IsDead && !unit.PlaysOnce)
            {
                return ++unit.TicksLyingDead <= TicksLyingDead;
            }

            var resting = unit.PauseTicksLeft > 0;
            if (resting)
            {
                unit.PauseTicksLeft--;
            }

            if (unit.Target == null)
            {
                LookForEnemy(unit);
            }

            if (unit.Target != null)
            {
                Pursue(unit, resting);
            }

            unit.Pose++;
            if (unit.PlaysOnce && unit.Pose >= unit.State.Frames.Count)
            {
                // The attack is over (its target is hit, and the unit rests), or the unit has fallen and now lies dead.
                if (!unit.IsDead)
                {
                    unit.PauseTicks = unit.Kind.Descriptor.AttackPauseOf(unit.State.Code) ?? 0;
                    unit.PauseTicksLeft = unit.PauseTicks;
                }

                // A blow lands as the attack ends; a shot does its damage where and when it lands.
                var fires = _catalog.ShotsOf(unit.Kind, unit.State).Count > 0;
                if (!unit.IsDead && !fires && unit.Target is { IsDead: false } hit)
                {
                    Hit(unit.Kind, unit.State.Code, hit);
                }

                SetFieldState(unit, unit.IsDead ? StateOf(unit.Kind, "DEATHLIE") ?? unit.State : StandStateOf(unit.Kind));
                unit.Pose = unit.IsDead ? unit.State.Frames.Count - 1 : 0;
                return true;
            }

            PlaySounds(unit);
            Fire(unit);
            if (unit.Movement.IsWalking)
            {
                StepAlongRoute(unit);
            }

            return true;
        }

        /// <summary>
        /// Sends the selected units to a point of the field: each turns towards its place in a block around the
        /// point, walks there and stands again. Units already walking keep their step.
        /// </summary>
        public void SendSelectedTo(Point target)
        {
            var group = SelectedFieldUnits;
            if (group.Count == 0)
            {
                return;
            }

            var spacing = group.Max(SpacingOf);
            var places = Formation.Around(ClampToField(target), group.Count, spacing).Select(ClampToField).ToList();
            var assignment = Formation.Assign(group.Select(unit => unit.Movement.Position).ToList(), places);

            for (var i = 0; i < group.Count; i++)
            {
                var unit = group[i];
                unit.Target = null;
                unit.Detours = 0;
                WalkTo(unit, places[assignment[i]], group.Contains);
            }

            UpdateFieldView();
        }

        /// <summary>
        /// One tick of the playback timer: the next frame of the animation view, and on the field the next frame
        /// of every unit and a step of every walking unit; a unit that arrives stands again.
        /// </summary>
        public void Tick()
        {
            if (!IsFieldMode)
            {
                return;
            }

            PlayImpacts();
            AdvanceEffects();
            LandShots();

            foreach (var unit in _fieldUnits.ToList())
            {
                if (!Advance(unit))
                {
                    RemoveFromField(unit);
                }
            }

            UpdateFieldView();
        }

        /// <summary>Plays the sounds the unit's state has at the frame it is in now.</summary>
        private void PlaySounds(FieldUnitView unit)
        {
            if (SoundBox.IsChecked != true || unit.State.Frames.Count == 0)
            {
                return;
            }

            var key = (unit.Kind, unit.State);
            if (!_stateSounds.TryGetValue(key, out var sounds))
            {
                sounds = _catalog.SoundsOf(unit.Kind, unit.State);
                _stateSounds[key] = sounds;
            }

            var frame = unit.Pose % unit.State.Frames.Count;
            foreach (var (_, sound, impact) in sounds.Where(sound => sound.Frame == frame))
            {
                if (impact)
                {
                    _impacts.Add((TicksToImpact, sound));
                }
                else
                {
                    Play(sound);
                }
            }
        }

        /// <summary>Plays the impacts whose time has come.</summary>
        private void PlayImpacts()
        {
            for (var i = _impacts.Count - 1; i >= 0; i--)
            {
                var (ticksLeft, sound) = _impacts[i];
                if (ticksLeft > 1)
                {
                    _impacts[i] = (ticksLeft - 1, sound);
                    continue;
                }

                _impacts.RemoveAt(i);
                if (SoundBox.IsChecked == true)
                {
                    Play(sound);
                }
            }
        }

        private void Play(GameSound sound)
        {
            if (sound.Files.Count == 0)
            {
                return;
            }

            var file = sound.Files[_random.Next(sound.Files.Count)];
            var path = SoundPath(file);
            if (path != null)
            {
                SoundPlayer.Play(path);
            }
            else if (StatusText.Text.Length == 0)
            {
                StatusText.Text = "No sound: " + file + " was not found around " + _catalog.Files.Source
                    + ". The sounds are in the game's Sound folder; open the All.gsc next to it to hear them.";
            }
        }

        /// <summary>A wave file of the sound list in the game folder: next to the archive, or around an extracted folder.</summary>
        private string SoundPath(string file)
        {
            var source = _catalog.Files.Source;
            var folders = new[] { System.IO.Path.GetDirectoryName(source), source, System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(source) ?? "") };

            return folders
                .Where(folder => !string.IsNullOrEmpty(folder))
                .Select(folder => System.IO.Path.Combine(folder, file.Replace('\\', System.IO.Path.DirectorySeparatorChar)))
                .FirstOrDefault(System.IO.File.Exists);
        }

        private void InitializeField()
        {
            UnitBox.SelectionChanged += (sender, args) =>
            {
                // Picking a unit puts one on the field; the list then clears, so the same unit can be picked again.
                if (!_updatingControls && UnitBox.SelectedItem is GameObject kind)
                {
                    AddToField(kind);
                    _updatingControls = true;
                    UnitBox.SelectedItem = null;
                    _updatingControls = false;
                }
            };
            RemoveUnitsButton.Click += (sender, args) => RemoveSelectedFromField();
            AttackButton.Click += (sender, args) => AttackWithSelected();
            KillButton.Click += (sender, args) => KillSelected();
            StandGroundBox.Click += (sender, args) => StandGroundWithSelected(StandGroundBox.IsChecked == true);
            PlayerColorBox.SelectionChanged += (sender, args) =>
            {
                // The color picker recolors the selected units (and is the color of units added next).
                if (IsFieldMode)
                {
                    foreach (var unit in SelectedFieldUnits)
                    {
                        unit.Color = PlayerColor;
                    }

                    UpdateFieldView();
                }
            };

            Field.PointerPressed += OnFieldPressed;
            Field.PointerMoved += OnFieldMoved;
            Field.PointerReleased += OnFieldReleased;
            KeyDown += (sender, args) =>
            {
                if (IsFieldMode && args.Key == Key.Delete && _fieldUnits.Any(unit => unit.IsSelected))
                {
                    RemoveSelectedFromField();
                    args.Handled = true;
                }
            };
        }

        /// <summary>Removes every unit and the frames rendered for them.</summary>
        private void ClearField()
        {
            foreach (var unit in _fieldUnits)
            {
                Field.Children.Remove(unit.Image);
                Field.Children.Remove(unit.Ring);
                Field.Children.Remove(unit.Marker);
                Field.Children.Remove(unit.LifeBar);
            }

            _fieldUnits.Clear();
            _readyFrames.Clear();
            _renderingFrames.Clear();
            foreach (var effect in _effects)
            {
                Field.Children.Remove(effect.Image);
            }

            _effects.Clear();
            _impactEffects.Clear();
            _shotsInFlight.Clear();
            _stateShots.Clear();
            _attackBoxKind = null;
            _stateSounds.Clear();
            _impacts.Clear();
            EndDrag();
        }

        /// <summary>The state a unit stands in: STAND, or the first state if it has none.</summary>
        private static GameObjectState StandStateOf(GameObject unit) =>
            unit.States.FirstOrDefault(state => state.Code == "STAND")
            ?? unit.States.FirstOrDefault(state => state.Code.StartsWith("STAND", StringComparison.Ordinal))
            ?? unit.States[0];

        /// <summary>The state a unit walks in: both walk steps, one of them, or standing if it cannot walk.</summary>
        private static GameObjectState WalkStateOf(GameObject unit) =>
            unit.States.FirstOrDefault(state => state.Code == "MOTION_L+MOTION_R")
            ?? unit.States.FirstOrDefault(state => state.Code == "MOTION_L")
            ?? unit.States.FirstOrDefault(state => state.Code.StartsWith("MOTION", StringComparison.Ordinal))
            ?? StandStateOf(unit);

        /// <summary>Puts a unit in a state; a new state starts from its first frame.</summary>
        private static void SetFieldState(FieldUnitView unit, GameObjectState state)
        {
            if (!ReferenceEquals(unit.State, state))
            {
                unit.State = state;
                unit.Pose = 0;
            }

            unit.PlaysOnce = false;
        }

        /// <summary>
        /// The attack to show: the first of the unit's attacks (ATTACK, ATTACK1, …) that sounds, such as a
        /// shot, or just its first attack; null if it has none.
        /// </summary>
        private GameObjectState AttackStateOf(GameObject unit)
        {
            var attacks = AttacksOf(unit);
            return attacks.FirstOrDefault(state => _catalog.SoundsOf(unit, state).Count > 0) ?? attacks.FirstOrDefault();
        }

        private static IReadOnlyList<GameObjectState> AttacksOf(GameObject unit)
        {
            return unit.States
                .Where(state => state.Code.StartsWith("ATTACK", StringComparison.Ordinal) && !state.Code.Contains('+'))
                .ToList();
        }

        /// <summary>Offers the attacks of the first selected unit, starting with its first choice.</summary>
        private void UpdateAttackBox()
        {
            var kind = _fieldUnits.FirstOrDefault(unit => unit.IsSelected)?.Kind;
            if (ReferenceEquals(kind, _attackBoxKind))
            {
                return;
            }

            _attackBoxKind = kind;
            var options = kind == null
                ? new List<AttackOption>()
                : AttacksOf(kind)
                    .Select(state => new AttackOption(
                          state
                        , kind.Descriptor.Weapons.FirstOrDefault(weapon => GameCatalog.SameAnimation(weapon.Animation, state.Code))?.Weapon
                    ))
                    .ToList();

            AttackBox.ItemsSource = options;
            AttackBox.SelectedItem = kind == null ? null : options.FirstOrDefault(option => ReferenceEquals(option.State, AttackStateOf(kind)));
            AttackBox.IsVisible = options.Count > 1;
        }

        /// <summary>The unit's state of a code, or the first whose code starts with it; null if it has none.</summary>
        private static GameObjectState StateOf(GameObject unit, string code) =>
            unit.States.FirstOrDefault(state => state.Code == code)
            ?? unit.States.FirstOrDefault(state => state.Code.StartsWith(code, StringComparison.Ordinal));

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

        /// <summary>The unit drawn at a point, the frontmost one if they overlap, or null.</summary>
        private FieldUnitView UnitAt(Point point)
        {
            return _fieldUnits
                .Where(unit => !unit.IsDead)
                .OrderByDescending(unit => unit.Movement.Position.Y)
                .FirstOrDefault(unit => unit.DrawnBounds is { } bounds
                    ? bounds.Inflate(3).Contains(point)
                    : Distance(unit.Movement.Position, point) <= 12);
        }

        /// <summary>Selects units: only these, or (with <paramref name="add"/>) these as well.</summary>
        private void Select(IEnumerable<FieldUnitView> units, bool add)
        {
            var chosen = units.ToHashSet();
            foreach (var unit in _fieldUnits)
            {
                unit.IsSelected = !unit.IsDead && (chosen.Contains(unit) || (add && unit.IsSelected));
            }

            UpdateFieldView();
        }

        private void OnFieldPressed(object sender, PointerPressedEventArgs e)
        {
            if (!IsFieldMode)
            {
                return;
            }

            Field.Focus();
            var point = e.GetCurrentPoint(Field);
            var add = (e.KeyModifiers & (KeyModifiers.Shift | KeyModifiers.Control)) != 0;

            if (point.Properties.IsLeftButtonPressed)
            {
                var hit = UnitAt(point.Position);
                if (hit != null)
                {
                    // Click a unit to select it; with Shift or Ctrl, to add it to the selection or take it out.
                    if (add)
                    {
                        hit.IsSelected = !hit.IsSelected;
                        UpdateFieldView();
                    }
                    else
                    {
                        Select(new[] { hit }, add: false);
                    }
                }
                else
                {
                    // On empty field: a click deselects, a drag selects the units in the box.
                    _dragStart = point.Position;
                    _dragging = false;
                    _dragAdds = add;
                    e.Pointer.Capture(Field);
                }

                e.Handled = true;
            }
            else if (point.Properties.IsRightButtonPressed)
            {
                // Right-click an enemy to attack it, anything else to go there.
                var enemy = UnitAt(point.Position);
                if (enemy != null && _fieldUnits.Any(unit => unit.IsSelected && IsEnemy(unit, enemy)))
                {
                    AttackUnit(enemy);
                }
                else
                {
                    SendSelectedTo(point.Position);
                }

                e.Handled = true;
            }
        }

        private void OnFieldMoved(object sender, PointerEventArgs e)
        {
            if (_dragStart is not { } start)
            {
                return;
            }

            var point = e.GetPosition(Field);
            if (!_dragging && Distance(start, point) < DragThreshold)
            {
                return;
            }

            _dragging = true;
            var box = Box(start, point);
            SelectionRectangle.Width = box.Width;
            SelectionRectangle.Height = box.Height;
            Canvas.SetLeft(SelectionRectangle, box.X);
            Canvas.SetTop(SelectionRectangle, box.Y);
            SelectionRectangle.IsVisible = true;
        }

        private void OnFieldReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_dragStart is not { } start || e.InitialPressMouseButton != MouseButton.Left)
            {
                return;
            }

            var point = e.GetPosition(Field);
            var dragged = _dragging || Distance(start, point) >= DragThreshold;
            var adds = _dragAdds;
            EndDrag();
            e.Pointer.Capture(null);

            if (dragged)
            {
                var box = Box(start, point);
                Select(_fieldUnits.Where(unit => unit.DrawnBounds is { } bounds
                    ? bounds.Intersects(box)
                    : box.Contains(unit.Movement.Position)), adds);
            }
            else if (!adds)
            {
                Select(Array.Empty<FieldUnitView>(), add: false);
            }

            e.Handled = true;
        }

        private void EndDrag()
        {
            _dragStart = null;
            _dragging = false;
            SelectionRectangle.IsVisible = false;
        }

        private static Rect Box(Point a, Point b) =>
            new Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

        /// <summary>Width of a unit's selection ring: about as wide as the unit.</summary>
        private static double RingWidthOf(FieldUnitView unit) => unit.RingWidth > 0 ? unit.RingWidth : 32;

        /// <summary>
        /// How far apart to place a unit in a group: about its size as drawn now, which for riders seen from the
        /// side is much wider than their ring (sized from the front).
        /// </summary>
        private static double SpacingOf(FieldUnitView unit) =>
            unit.Frame?.Opaque is { } opaque
                ? Math.Max(24, Math.Max(Math.Max(opaque.Width, opaque.Height) * 0.9, RingWidthOf(unit) + 8))
                : RingWidthOf(unit) + 8;

        /// <summary>Draws every unit: its current frame with its feet at its position, its ring and target.</summary>
        private void UpdateFieldView()
        {
            if (!IsFieldMode)
            {
                FieldHint.Text = "Open the game's All.gsc (or a folder extracted from it) to put its units on the field.";
                RemoveUnitsButton.IsEnabled = false;
                AttackButton.IsEnabled = false;
                KillButton.IsEnabled = false;
                return;
            }

            foreach (var effect in _effects)
            {
                DrawSprite(effect);
                effect.Image.ZIndex++;
            }

            foreach (var unit in _fieldUnits)
            {
                var feet = DrawSprite(unit);

                // A unit's ring is just behind it, over the units behind.
                unit.Ring.ZIndex = unit.Image.ZIndex - 1;
                unit.Marker.ZIndex = 1;

                if (unit.RingWidth <= 0 && unit.Frame?.Opaque is { } opaque)
                {
                    unit.RingWidth = Math.Round(Math.Clamp(opaque.Width * 0.9, 20, 160));
                }

                var ringWidth = RingWidthOf(unit);
                var ringHeight = Math.Round(Math.Max(8, ringWidth * 0.45));
                unit.Ring.IsVisible = unit.IsSelected;
                unit.Ring.Width = ringWidth;
                unit.Ring.Height = ringHeight;
                Canvas.SetLeft(unit.Ring, feet.X - ringWidth / 2);
                Canvas.SetTop(unit.Ring, feet.Y - ringHeight / 2);

                var barsTop = (unit.DrawnBounds?.Top ?? feet.Y - 40) - 6;
                ShowBar(
                      unit.LifeBar
                    , !unit.IsDead && unit.Life < unit.FullLife
                    , unit.Life / (double) unit.FullLife
                    , new Point(feet.X, barsTop)
                );
                ShowBar(
                      unit.ReloadBar
                    , !unit.IsDead && unit.Kind.Descriptor.ShowsDelay && unit.PauseTicksLeft > 0
                    , 1 - unit.PauseTicksLeft / (double) Math.Max(1, unit.PauseTicks)
                    , new Point(feet.X, barsTop + 4)
                );

                var target = unit.Goal ?? unit.Movement.Target;
                unit.Marker.IsVisible = unit.IsSelected && target != null;
                if (target is { } t)
                {
                    Canvas.SetLeft(unit.Marker, t.X - unit.Marker.Width / 2);
                    Canvas.SetTop(unit.Marker, t.Y - unit.Marker.Height / 2);
                }
            }

            var selected = _fieldUnits.Count(unit => unit.IsSelected);
            RemoveUnitsButton.IsEnabled = selected > 0;
            AttackButton.IsEnabled = selected > 0;
            KillButton.IsEnabled = selected > 0;

            var holders = _fieldUnits.Where(unit => unit.IsSelected && unit.Kind.Descriptor.CanStandGround).ToList();
            StandGroundBox.IsEnabled = holders.Count > 0;
            StandGroundBox.IsChecked = holders.Count > 0 && holders.All(unit => unit.StandsGround);
            UpdateAttackBox();
            FieldHint.Text = selected == 0
                ? "Click a unit, or drag a box around several, to select them (Shift adds to the selection). Pick a unit in the list to put another one on the field."
                : selected + (selected == 1 ? " unit" : " units") + " selected: right-click where to go, or an enemy (a unit of another color) to attack it. Delete removes them.";
        }

        /// <summary>Shows a bar filled to a share of its width, its top middle at a point; or hides it.</summary>
        private static void ShowBar(Rectangle bar, bool visible, double share, Point topMiddle)
        {
            bar.IsVisible = visible;
            if (visible)
            {
                bar.Width = Math.Max(1, Math.Round(LifeBarWidth * Math.Clamp(share, 0, 1)));
                Canvas.SetLeft(bar, topMiddle.X - LifeBarWidth / 2);
                Canvas.SetTop(bar, topMiddle.Y);
            }
        }

        /// <summary>
        /// Draws the current frame of a unit or an effect with its anchor at its position, in front of those
        /// further up the field. Returns that position in whole pixels.
        /// </summary>
        private Point DrawSprite(FieldUnitView unit)
        {
            var key = new FieldFramesKey(
                  unit.Kind
                , unit.State
                , unit.Direction
                , unit.Color.Offset
                , unit.Fog?.ToArgb() ?? 0
            );
            if (_readyFrames.TryGetValue(key, out var frames))
            {
                unit.Shown = frames;
            }
            else
            {
                StartRendering(key, unit);
            }

            var shown = unit.Shown;
            unit.Frame = shown != null && shown.Images.Count > 0 ? shown.Images[unit.Pose % shown.Images.Count] : null;
            unit.Image.Source = unit.Frame?.Bitmap;

            var feet = new Point(Math.Round(unit.Movement.Position.X), Math.Round(unit.Movement.Position.Y));
            if (shown != null)
            {
                unit.Origin = new Point(feet.X - shown.Anchor.X, feet.Y - shown.Anchor.Y);
                Canvas.SetLeft(unit.Image, unit.Origin.X);
                Canvas.SetTop(unit.Image, unit.Origin.Y);
            }

            unit.Image.ZIndex = 2 * (1000 + (int) feet.Y);
            return feet;
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

            var key = (unit.Kind, unit.State);
            if (!_stateShots.TryGetValue(key, out var shots))
            {
                shots = _catalog.ShotsOf(unit.Kind, unit.State);
                _stateShots[key] = shots;
            }

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
                var ahead = new Vector(-Math.Sin(heading), Math.Cos(heading));
                var reach = ShotReachOf(unit, unit.State.Code) / FieldPath.GroundDistance(default, new Point(ahead.X, ahead.Y));
                var aim = unit.Target != null
                    ? AimAt(unit.Target, takesTime ? TicksToImpact : 0)
                    : ClampToField(unit.Movement.Position + ahead * reach);
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
                        projectile.Flight = (from, end, height, TicksToImpact);
                    }
                }

                if (impact.Count > 0)
                {
                    _impactEffects.Add((TicksToImpact, impact[_random.Next(impact.Count)], where));
                }

                if (takesTime)
                {
                    _shotsInFlight.Add(new ShotInFlight(
                          TicksToImpact
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
                    effect.Movement.PlaceAt(flight.From + (flight.To - flight.From) * done + new Vector(0, -4 * flight.Height * done * (1 - done)));
                    over = effect.FlightAge >= flight.Ticks;
                }

                if (over)
                {
                    Field.Children.Remove(effect.Image);
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

        /// <summary>Renders the frames for a unit's state and direction in the background, then draws again.</summary>
        private void StartRendering(FieldFramesKey key, FieldUnitView unit)
        {
            if (_renderingFrames.ContainsKey(key))
            {
                return;
            }

            TrimFrameCache();

            var request = new RenderRequest(
                  _catalog
                , unit.Kind
                , unit.State
                , unit.Directions
                , key.Direction
                , unit.Color
                , unit.Fog
            );
            var task = RenderFieldFramesAsync(key, request);
            if (!task.IsCompleted)
            {
                _renderingFrames[key] = task;
            }

            RenderingCompleted = Task.WhenAll(RenderingCompleted, task);
        }

        private async Task RenderFieldFramesAsync(FieldFramesKey key, RenderRequest request)
        {
            try
            {
                var (results, anchor) = await Task.Run(() => Render(request, _palette));

                // The game data changed meanwhile: these frames are for units no longer on the field.
                if (!ReferenceEquals(request.Catalog, _catalog))
                {
                    return;
                }

                var status = StatusOf(results);
                if (status.Length > 0)
                {
                    StatusText.Text = request.Unit.Label + ", " + request.State.Label + ": " + status;
                }

                _readyFrames[key] = new FieldFrames(
                    results
                        .Where(result => result.Image != null)
                        .Select(result => new RenderedImage(result.Number, result.Image.ToWriteableBitmap(), result.Opaque))
                        .ToList(),
                    anchor);
            }
            finally
            {
                _renderingFrames.Remove(key);
            }

            UpdateFieldView();
        }

        /// <summary>Keeps rendered frames from piling up: drops those no unit shows now.</summary>
        private void TrimFrameCache()
        {
            if (_readyFrames.Count < 200)
            {
                return;
            }

            var inUse = _fieldUnits.Select(unit => unit.Shown).ToHashSet();
            foreach (var key in _readyFrames.Where(pair => !inUse.Contains(pair.Value)).Select(pair => pair.Key).ToList())
            {
                _readyFrames.Remove(key);
            }
        }
    }
}
