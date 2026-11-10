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

        private static Rectangle Bar(double height, IBrush fill) =>
            new Rectangle { Height = height, Fill = fill, IsHitTestVisible = false, IsVisible = false, ZIndex = 99999 };

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
