using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Viewer
{
    /// <summary>One thing drawn on the field: an image, or else an ellipse or a rectangle, within its bounds.</summary>
    internal readonly record struct FieldShape(
          int ZIndex
        , Rect Bounds
        , IImage Image = null
        , IBrush Fill = null
        , IPen Pen = null
        , bool IsRound = false
    );

    /// <summary>Draws everything on the field at once, back to front.</summary>
    internal sealed class FieldLayer : Control
    {
        private FieldShape[] _shapes = System.Array.Empty<FieldShape>();

        /// <summary>Shows the shapes, those with a higher <see cref="FieldShape.ZIndex"/> over the others.</summary>
        public void Show(IEnumerable<FieldShape> shapes)
        {
            _shapes = shapes.OrderBy(shape => shape.ZIndex).ToArray();
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            foreach (var shape in _shapes)
            {
                if (shape.Image != null)
                {
                    context.DrawImage(shape.Image, shape.Bounds);
                }
                else if (shape.IsRound)
                {
                    // The outline stays within the bounds.
                    var inside = shape.Bounds.Deflate((shape.Pen?.Thickness ?? 0) / 2);
                    context.DrawEllipse(
                          shape.Fill
                        , shape.Pen
                        , inside.Center
                        , inside.Width / 2
                        , inside.Height / 2
                    );
                }
                else
                {
                    context.DrawRectangle(shape.Fill, shape.Pen, shape.Bounds);
                }
            }
        }
    }
}
