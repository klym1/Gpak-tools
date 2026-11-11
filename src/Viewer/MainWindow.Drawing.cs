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
    /// <summary>Drawing the field: every unit and effect, rendered in the background and cached.</summary>
    public partial class MainWindow
    {
        private readonly record struct FieldFramesKey(GameObject Kind, GameObjectState State, int Direction, int Color, int Fog);

        private const int BarZIndex = 99999;
        private const int GroundZIndex = 1;

        private static readonly IPen RingPen = new Pen(RingBrush, 2);
        private static readonly IPen MarkerPen = new Pen(Brushes.White, 1.5);
        private static readonly Size MarkerSize = new Size(14, 7);
        private static readonly Size ShadowSize = new Size(6, 3);
        private static readonly IBrush ShadowBrush = new SolidColorBrush(Colors.Black, 0.35);

        private readonly List<FieldShape> _shapes = new List<FieldShape>();

        private static Rect Around(Point centre, Size size) =>
            new Rect(centre.X - size.Width / 2, centre.Y - size.Height / 2, size.Width, size.Height);

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

        /// <summary>The formation list offers what the selected units may stand in, by the nations' rules.</summary>
        private void UpdateFormationBox()
        {
            var allowed = _catalog.FormationsFor(_fieldUnits
                .Where(unit => unit.IsSelected && !unit.Kind.IsBuilding)
                .Select(unit => unit.Kind));
            if (allowed.SequenceEqual(_offeredFormations))
            {
                return;
            }

            var updating = _updatingControls;
            _updatingControls = true;
            OfferFormations(allowed);
            _updatingControls = updating;
        }

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

            _shapes.Clear();
            foreach (var effect in _effects)
            {
                DrawSprite(effect, 1, true);
                if (effect.Shadow is { } shadow)
                {
                    _shapes.Add(new FieldShape(GroundZIndex + 1, Around(shadow, ShadowSize), Fill: ShadowBrush, IsRound: true));
                }
            }

            foreach (var unit in _fieldUnits)
            {
                unit.IsShown = IsSeen(unit);
                var (feet, zIndex) = DrawSprite(unit, 0, unit.IsShown);
                if (!unit.IsShown)
                {
                    unit.LifeBar = default;
                    unit.ReloadBar = default;
                    continue;
                }

                if (unit.RingWidth <= 0 && unit.Frame?.Opaque is { } opaque)
                {
                    unit.RingWidth = Math.Round(Math.Clamp(opaque.Width * 0.9, 20, 160));
                }

                if (unit.IsSelected)
                {
                    // A unit's ring is just behind it, over the units behind.
                    var ringWidth = RingWidthOf(unit);
                    var ringSize = new Size(ringWidth, Math.Round(Math.Max(8, ringWidth * 0.45)));
                    _shapes.Add(new FieldShape(zIndex - 1, Around(feet, ringSize), Pen: RingPen, IsRound: true));

                    if ((unit.Goal ?? unit.Movement.Target) is { } target)
                    {
                        _shapes.Add(new FieldShape(GroundZIndex, Around(target, MarkerSize), Pen: MarkerPen, IsRound: true));
                    }
                }

                var barsTop = (unit.DrawnBounds?.Top ?? feet.Y - 40) - 6;
                unit.LifeBar = ShowBar(
                      !unit.IsDead && unit.Life < unit.FullLife
                    , unit.Life / (double) unit.FullLife
                    , new Point(feet.X, barsTop)
                    , 3
                    , RingBrush
                );
                unit.ReloadBar = ShowBar(
                      !unit.IsDead && unit.Kind.Descriptor.ShowsDelay && unit.PauseTicksLeft > 0
                    , 1 - unit.PauseTicksLeft / (double) Math.Max(1, unit.PauseTicks)
                    , new Point(feet.X, barsTop + 4)
                    , 2
                    , Brushes.White
                );
            }

            Sprites.Show(_shapes);
            ArmiesText.Text = ArmiesSummary();

            var selected = _fieldUnits.Count(unit => unit.IsSelected);
            RemoveUnitsButton.IsEnabled = selected > 0;
            AttackButton.IsEnabled = selected > 0;
            KillButton.IsEnabled = selected > 0;

            var holders = _fieldUnits.Where(unit => unit.IsSelected && unit.Kind.Descriptor.CanStandGround).ToList();
            StandGroundBox.IsEnabled = holders.Count > 0;
            StandGroundBox.IsChecked = holders.Count > 0 && holders.All(unit => unit.StandsGround);
            UpdateAttackBox();
            UpdateFormationBox();
            FieldHint.Text = selected == 0
                ? "Click a unit, or drag a box around several, to select them (Shift adds to the selection). Pick a unit in the list to put another one on the field."
                : selected + (selected == 1 ? " unit" : " units") + " selected: right-click where to go, or an enemy (a unit of another color) to attack it. Delete removes them.";
        }

        /// <summary>Shows a bar filled to a share of its width, its top middle at a point; or hides it.</summary>
        private FieldBar ShowBar(
              bool visible
            , double share
            , Point topMiddle
            , double height
            , IBrush fill
        )
        {
            if (!visible)
            {
                return default;
            }

            var width = Math.Max(1, Math.Round(LifeBarWidth * Math.Clamp(share, 0, 1)));
            _shapes.Add(new FieldShape(
                  BarZIndex
                , new Rect(topMiddle.X - LifeBarWidth / 2, topMiddle.Y, width, height)
                , Fill: fill
            ));
            return new FieldBar(true, width);
        }

        /// <summary>
        /// Draws the current frame of a unit or an effect with its anchor at its position, in front of those
        /// further up the field, and by <paramref name="lift"/> in front of those level with it. Returns that
        /// position in whole pixels, and how far in front it is drawn.
        /// </summary>
        private (Point Feet, int ZIndex) DrawSprite(FieldUnitView unit, int lift, bool shown)
        {
            var key = new FieldFramesKey(
                  unit.Kind
                , unit.State
                , unit.Direction
                , unit.Color.Offset
                , unit.Fog?.ToArgb() ?? 0
            );
            if (_readyFrames.TryGetValue(key, out var ready))
            {
                unit.Shown = ready;
            }
            else
            {
                StartRendering(key, unit);
            }

            var frames = unit.Shown;
            unit.Frame = frames != null && frames.Images.Count > 0 ? frames.Images[unit.Pose % frames.Images.Count] : null;

            var feet = new Point(Math.Round(unit.Movement.Position.X), Math.Round(unit.Movement.Position.Y));
            if (frames != null)
            {
                unit.Origin = new Point(feet.X - frames.Anchor.X, feet.Y - frames.Anchor.Y);
            }

            var zIndex = 2 * (1000 + (int) feet.Y) + lift;
            if (shown && unit.Frame?.Bitmap is { } bitmap)
            {
                _shapes.Add(new FieldShape(zIndex, new Rect(unit.Origin, bitmap.Size), bitmap));
            }

            return (feet, zIndex);
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
