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

        /// <summary>How long, in ticks, a unit stands idle before it takes a rest: somewhere between these.</summary>
        private const int MinTicksToRest = 150;
        private const int MaxTicksToRest = 400;

        /// <summary>The <c>GEOMETRY</c> step of a foot soldier, who walks <see cref="FieldUnit.DefaultStepLength"/> a frame.</summary>
        private const double FootStepSize = 32;

        /// <summary><c>ROTATE</c> is taken as parts of a turn per tick, a full turn being this many.</summary>
        private const double TurnParts = 256;

        /// <summary><c>RAZBROS</c> is how far, in pixels each way, a shot at this distance may land from its aim.</summary>
        private const double ScatterDistance = 2000;

        /// <summary>How far, in pixels, an enemy moves before the way to it is planned again.</summary>
        private const double RetargetDistance = 12;

        /// <summary>Ticks a unit waits behind one walking its way, and ways round it tries, before something else is done.</summary>
        private const int MaxWaitTicks = 25;
        private const int MaxDetours = 6;

        /// <summary>Steps in a row with nothing in its way after which a unit is past what it went round.</summary>
        private const int StepsToGetOn = 8;

        /// <summary>How long, in ticks, a unit kept from its place waits before it tries the way again: from the first to the longest wait.</summary>
        private const int MinRetryTicks = 8;
        private const int MaxRetryTicks = 96;

        /// <summary>How long after the shot a weapon lands, in ticks.</summary>
        private const int TicksToImpact = 20;

        private readonly List<(int TicksLeft, GameSound Sound)> _impacts = new();

        // Weapon animations playing on the field (smoke, explosions), and those still to appear where a shot lands.
        private readonly List<FieldUnitView> _effects = new();
        private readonly List<(int TicksLeft, WeaponEffect Effect, Point Where)> _impactEffects = new();
        private readonly Dictionary<(GameObject, GameObjectState), IReadOnlyList<GameObjectShot>> _stateShots = new();

        // Shots on their way: they do their damage where they land, to whoever is there then.
        private readonly List<ShotInFlight> _shotsInFlight = new();

        /// <summary>How far, in pixels on the ground, a shot without a blast may land from a unit's footprint and still hit it.</summary>
        private const double HitSlack = 6;

        /// <summary>The weapon animations playing on the field now.</summary>
        public IReadOnlyList<FieldUnitView> Effects => _effects;
        private readonly Random _random = new Random();

        /// <summary>What plays the units' sounds.</summary>
        public ISoundPlayer SoundPlayer { get; set; } = SoundPlayers.ForThisSystem();

        /// <summary>How long a killed unit lies on the field, in ticks.</summary>
        private const int TicksLyingDead = 100;

        // A selection box being dragged.
        private static readonly Vector PasteShift = new Vector(24, 24);

        // The copied units, each with its place relative to the middle of them all.
        private readonly List<(GameObject Kind, NationColorOffset Color, Point Offset, double Heading, bool StandsGround)> _copiedUnits = new();
        private Point _copiedMiddle;

        // Where the pointer is on the field; null while it is off it.
        private Point? _pointer;

        private Point? _dragStart;
        private bool _dragging;
        private bool _dragAdds;

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
            catalog?.Units.Concat(catalog.Buildings).FirstOrDefault(o => o.Descriptor.FileName.Equals(descriptorName, StringComparison.OrdinalIgnoreCase));

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

        /// <summary>Remembers the selected units, to paste copies of them.</summary>
        public void CopySelected()
        {
            var selected = SelectedFieldUnits;
            if (selected.Count == 0)
            {
                return;
            }

            _copiedMiddle = new Point(
                  selected.Average(unit => unit.Movement.Position.X)
                , selected.Average(unit => unit.Movement.Position.Y)
            );
            _copiedUnits.Clear();
            _copiedUnits.AddRange(selected.Select(unit => (
                  unit.Kind
                , unit.Color
                , unit.Movement.Position - _copiedMiddle
                , unit.Movement.Heading
                , unit.StandsGround
            )));
        }

        /// <summary>
        /// Puts copies of the copied units on the field, placed as they stood, around the pointer (or a little
        /// aside of where they were copied, with the pointer off the field); the copies become the selection.
        /// </summary>
        public IReadOnlyList<FieldUnitView> PasteCopied()
        {
            var middle = _pointer ?? _copiedMiddle + PasteShift;
            _copiedMiddle = middle;
            var pasted = new List<FieldUnitView>();
            foreach (var (kind, color, offset, heading, standsGround) in _copiedUnits)
            {
                var unit = AddToField(
                      kind
                    , ClampToField(middle + offset)
                    , color
                    , heading
                );
                unit.StandsGround = standsGround;
                pasted.Add(unit);
            }

            Select(pasted, add: false);
            return pasted;
        }

        private FieldUnitView AddToField(
              GameObject kind
            , Point? at = null
            , NationColorOffset? color = null
            , double heading = 0
        )
        {
            var descriptor = kind.Descriptor;
            var movement = new FieldUnit(at ?? FreePlace(), heading)
            {
                StepLength = FieldUnit.DefaultStepLength * (descriptor.StepSize is > 0 and var step ? step : FootStepSize) / FootStepSize,
                TurnRate = descriptor.TurnRate * 360 / TurnParts,
                TurnsUnderWay = descriptor.TurnsUnderWay,
            };

            var unit = new FieldUnitView(kind, color ?? PlayerColor, StandStateOf(kind), movement);
            _fieldUnits.Add(unit);

            Select(new[] { unit }, add: false);
            Play(descriptor.BornSound);
            Score(unit.Color, WorthOf(unit));
            return unit;
        }

        /// <summary>Removes the selected units from the field.</summary>
        public void RemoveSelectedFromField()
        {
            foreach (var unit in SelectedFieldUnits)
            {
                RemoveFromField(unit);
            }

            UpdateFieldView();
        }

        internal void RemoveFromField(FieldUnitView unit)
        {
            _fieldUnits.Remove(unit);
        }

        private void PlayOnce(FieldUnitView unit, GameObjectState state)
        {
            unit.State = state;
            unit.Pose = 0;
            unit.PlaysOnce = true;
            Fire(unit);
            PlaySounds(unit);
        }

        /// <summary>The unit's next frame; false when it has lain dead long enough and leaves the field.</summary>
        private bool Advance(FieldUnitView unit)
        {
            if (unit.IsDead && !unit.PlaysOnce)
            {
                if (++unit.TicksLyingDead <= TicksLyingDead)
                {
                    return true;
                }

                // The body decays through the stages the unit has (DEATHLIE1, DEATHLIE2, …), then it is gone.
                var decayed = NextCorpseOf(unit);
                if (decayed == null)
                {
                    return false;
                }

                SetFieldState(unit, decayed);
                unit.TicksLyingDead = 0;
                return true;
            }

            RestWhenIdle(unit);

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
                var wasAttack = IsAttack(unit.State);
                if (!unit.IsDead && wasAttack)
                {
                    unit.PauseTicks = unit.Kind.Descriptor.AttackPauseOf(unit.State.Code) ?? 0;
                    unit.PauseTicksLeft = unit.PauseTicks;
                }

                // A blow lands as the attack ends; a shot does its damage where and when it lands.
                var fires = ShotsOf(unit.Kind, unit.State).Count > 0;
                if (!unit.IsDead && wasAttack && !fires && unit.Target is { IsDead: false } hit)
                {
                    Hit(unit, unit.State.Code, hit);
                }

                if (unit.IsDead)
                {
                    SetFieldState(unit, StateOf(unit.Kind, "DEATHLIE") ?? unit.State);
                    unit.Pose = unit.State.Frames.Count - 1;
                }
                else if (unit.Next.Count > 0)
                {
                    PlayOnce(unit, unit.Next.Dequeue());
                }
                else if (wasAttack && unit.Target == null)
                {
                    // Nobody to fight: the weapon is lowered again.
                    Unready(unit);
                }
                else
                {
                    SetFieldState(unit, IdleStateOf(unit));
                    unit.Pose = 0;
                }

                return true;
            }

            Fire(unit);
            PlaySounds(unit);
            if (unit.Movement.IsWalking)
            {
                StepAlongRoute(unit);
            }
            else if (unit.Target == null)
            {
                // A slow unit at its place in a formation turns to its front.
                unit.Movement.Turn();
                RetryWay(unit);
            }

            return true;
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

        private void InitializeField()
        {
            foreach (var box in new[] { UnitBox, BuildingBox })
            {
                box.SelectionChanged += (sender, args) =>
                {
                    // Picking a unit puts one on the field; the list then clears, so the same unit can be picked again.
                    if (!_updatingControls && box.SelectedItem is GameObject kind)
                    {
                        AddToField(kind);
                        _updatingControls = true;
                        box.SelectedItem = null;
                        _updatingControls = false;
                    }
                };
            }
            FormationSpacingBox.Value = DefaultFormationSpacing;
            FormationBox.SelectionChanged += (sender, args) => FormUpWhenPicked();
            FormationSpacingBox.ValueChanged += (sender, args) => FormUpWhenPicked();
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
            Field.PointerExited += (sender, args) => _pointer = null;
            KeyDown += (sender, args) =>
            {
                if (!IsFieldMode)
                {
                    return;
                }

                // In a text box Ctrl+C and Ctrl+V are the text's.
                var command = (args.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && args.Source is not TextBox;
                if (args.Key == Key.Delete && _fieldUnits.Any(unit => unit.IsSelected))
                {
                    RemoveSelectedFromField();
                    args.Handled = true;
                }
                else if (command && args.Key == Key.C && _fieldUnits.Any(unit => unit.IsSelected))
                {
                    CopySelected();
                    args.Handled = true;
                }
                else if (command && args.Key == Key.V && _copiedUnits.Count > 0)
                {
                    PasteCopied();
                    args.Handled = true;
                }
            };
        }

        private void FormUpWhenPicked()
        {
            if (!_updatingControls && IsFieldMode && FormationBox.SelectedItem is GameFormation)
            {
                FormUpSelected();
            }
        }

        /// <summary>Removes every unit and the frames rendered for them.</summary>
        private void ClearField()
        {
            _fieldUnits.Clear();
            _copiedUnits.Clear();
            _readyFrames.Clear();
            _renderingFrames.Clear();
            _effects.Clear();
            _impactEffects.Clear();
            _shotsInFlight.Clear();
            _stateShots.Clear();
            _attackBoxKind = null;
            _stateSounds.Clear();
            _attackStates.Clear();
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
            ?? unit.States.FirstOrDefault(state => state.Code.StartsWith("MOTION_L0+", StringComparison.Ordinal))
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

        /// <summary>The unit's state of a code, or the first whose code starts with it; null if it has none.</summary>
        private static GameObjectState StateOf(GameObject unit, string code) =>
            unit.States.FirstOrDefault(state => state.Code == code)
            ?? unit.States.FirstOrDefault(state => state.Code.StartsWith(code, StringComparison.Ordinal));

        /// <summary>The unit drawn at a point, the frontmost one if they overlap, or null.</summary>
        private FieldUnitView UnitAt(Point point)
        {
            return _fieldUnits
                .Where(unit => !unit.IsDead && IsSeen(unit))
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
            _pointer = point.Position;
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

                    PlayOfSelected(descriptor => descriptor.SelectSound);
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

                PlayOfSelected(descriptor => descriptor.OrderSound);
                e.Handled = true;
            }
        }

        private void OnFieldMoved(object sender, PointerEventArgs e)
        {
            var point = e.GetPosition(Field);
            _pointer = point;
            if (_dragStart is not { } start)
            {
                return;
            }

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
                PlayOfSelected(descriptor => descriptor.SelectSound);
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
    }
}
