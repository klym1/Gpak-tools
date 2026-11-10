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

        private static readonly IBrush RingBrush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(0x9C, 0xFF, 0x57));

        private readonly List<FieldUnitView> _fieldUnits = new List<FieldUnitView>();

        // Frames of a unit kind's state from one direction in one color: ready, or being rendered.
        private readonly Dictionary<FieldFramesKey, FieldFrames> _readyFrames = new Dictionary<FieldFramesKey, FieldFrames>();
        private readonly Dictionary<FieldFramesKey, Task> _renderingFrames = new Dictionary<FieldFramesKey, Task>();

        // A selection box being dragged.
        private Point? _dragStart;
        private bool _dragging;
        private bool _dragAdds;

        private readonly record struct FieldFramesKey(GameObject Kind, GameObjectState State, int Direction, int Color);

        /// <summary>Whether the field is shown instead of the animation of one unit or sprite.</summary>
        public bool IsFieldMode => FieldButton.IsChecked == true && _catalog != null;

        /// <summary>Units on the field, in the order they were added.</summary>
        public IReadOnlyList<FieldUnitView> FieldUnits => _fieldUnits;

        public IReadOnlyList<FieldUnitView> SelectedFieldUnits => _fieldUnits.Where(unit => unit.IsSelected).ToList();

        /// <summary>Shows or hides the field. The first time it gets the picked unit, standing in the middle.</summary>
        public Task SetFieldMode(bool on)
        {
            FieldButton.IsChecked = on;
            return RenderingCompleted;
        }

        /// <summary>
        /// Adds the unit picked in the unit list to the field in the picked color, standing and facing the
        /// viewer, at <paramref name="at"/> or in a free place near the middle; it becomes the selection.
        /// </summary>
        public FieldUnitView AddToField(Point? at = null)
        {
            var kind = UnitBox.SelectedItem as GameObject ?? _catalog?.Units.FirstOrDefault();
            if (kind == null)
            {
                return null;
            }

            var unit = new FieldUnitView(kind, PlayerColor, StandStateOf(kind), new FieldUnit(at ?? FreePlace()))
            {
                Ring = new Ellipse { Stroke = RingBrush, StrokeThickness = 2, IsHitTestVisible = false, IsVisible = false },
                Marker = new Ellipse { Width = 14, Height = 7, Stroke = Brushes.White, StrokeThickness = 1.5, IsHitTestVisible = false, IsVisible = false },
            };

            Field.Children.Add(unit.Ring);
            Field.Children.Add(unit.Marker);
            Field.Children.Add(unit.Image);
            _fieldUnits.Add(unit);

            Select(new[] { unit }, add: false);
            return unit;
        }

        /// <summary>Removes the selected units from the field.</summary>
        public void RemoveSelectedFromField()
        {
            foreach (var unit in SelectedFieldUnits)
            {
                Field.Children.Remove(unit.Image);
                Field.Children.Remove(unit.Ring);
                Field.Children.Remove(unit.Marker);
                _fieldUnits.Remove(unit);
            }

            UpdateFieldView();
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
                unit.Movement.MoveTo(places[assignment[i]]);
                SetFieldState(unit, unit.Movement.IsWalking ? WalkStateOf(unit.Kind) : StandStateOf(unit.Kind));
            }

            Play();
            UpdateFieldView();
        }

        /// <summary>
        /// One tick of the playback timer: the next frame of the animation view, and on the field the next frame
        /// of every unit and a step of every walking unit; a unit that arrives stands again.
        /// </summary>
        public void Tick()
        {
            StepFrame(1);

            if (!IsFieldMode)
            {
                return;
            }

            foreach (var unit in _fieldUnits)
            {
                unit.Pose++;
                if (unit.Movement.IsWalking && unit.Movement.Step())
                {
                    SetFieldState(unit, StandStateOf(unit.Kind));
                }
            }

            UpdateFieldView();
        }

        private void InitializeField()
        {
            FieldButton.IsCheckedChanged += (sender, args) =>
            {
                if (!_updatingControls)
                {
                    UpdateFieldControls();
                }
            };
            AddUnitButton.Click += (sender, args) => AddToField();
            RemoveUnitsButton.Click += (sender, args) => RemoveSelectedFromField();
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

        /// <summary>Shows the field or the animation view, and the controls that work with each.</summary>
        private void UpdateFieldControls()
        {
            var on = IsFieldMode;
            ImageScroller.IsVisible = !on;
            Field.IsVisible = on;
            AddUnitButton.IsVisible = on;
            RemoveUnitsButton.IsVisible = on;

            // On the field every unit has its own state, direction and frame.
            StateBox.IsEnabled = !on;
            DirectionPanel.IsEnabled = !on;
            PreviousButton.IsEnabled = !on;
            NextButton.IsEnabled = !on;
            FrameText.IsVisible = !on;
            FromFrame.IsEnabled = !on;
            ToFrame.IsEnabled = !on;

            if (!on)
            {
                return;
            }

            if (_fieldUnits.Count == 0)
            {
                AddToField();
            }

            Play();
            UpdateFieldView();
        }

        /// <summary>Removes every unit and the frames rendered for them.</summary>
        private void ClearField()
        {
            foreach (var unit in _fieldUnits)
            {
                Field.Children.Remove(unit.Image);
                Field.Children.Remove(unit.Ring);
                Field.Children.Remove(unit.Marker);
            }

            _fieldUnits.Clear();
            _readyFrames.Clear();
            _renderingFrames.Clear();
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

        /// <summary>The place nearest the middle of the field that is not taken by another unit.</summary>
        private Point FreePlace()
        {
            var size = FieldSize();
            var middle = size.Width > 0 && size.Height > 0
                ? new Point(Math.Round(size.Width / 2), Math.Round(size.Height * 0.6))
                : new Point(240, 200);

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
                unit.IsSelected = chosen.Contains(unit) || (add && unit.IsSelected);
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
                SendSelectedTo(point.Position);
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
                return;
            }

            foreach (var unit in _fieldUnits)
            {
                var key = new FieldFramesKey(unit.Kind, unit.State, unit.Direction, unit.Color.Offset);
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

                // Units further down the field are in front; a unit's ring is just behind it, over the units behind.
                unit.Image.ZIndex = 2 * (1000 + (int) feet.Y);
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

                var target = unit.Movement.Target;
                unit.Marker.IsVisible = unit.IsSelected && target != null;
                if (target is { } t)
                {
                    Canvas.SetLeft(unit.Marker, t.X - unit.Marker.Width / 2);
                    Canvas.SetTop(unit.Marker, t.Y - unit.Marker.Height / 2);
                }
            }

            var selected = _fieldUnits.Count(unit => unit.IsSelected);
            RemoveUnitsButton.IsEnabled = selected > 0;
            FieldHint.Text = selected == 0
                ? "Click a unit, or drag a box around several, to select them (Shift adds to the selection). \"Add unit\" puts another one on the field."
                : selected + (selected == 1 ? " unit" : " units") + " selected: right-click where to go. Delete removes them.";
        }

        /// <summary>Renders the frames for a unit's state and direction in the background, then draws again.</summary>
        private void StartRendering(FieldFramesKey key, FieldUnitView unit)
        {
            if (_renderingFrames.ContainsKey(key))
            {
                return;
            }

            TrimFrameCache();

            var request = new RenderRequest(null, _catalog, unit.Kind, unit.State, unit.Directions, key.Direction, unit.Color, Transparent: true);
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
