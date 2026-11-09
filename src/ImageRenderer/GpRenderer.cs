using System;
using System.Collections.Generic;
using System.Drawing;
using GPExtractor;
using Types;

namespace ImageRenderer
{
    /// <summary>The image of one frame, plus the parts that could not be drawn.</summary>
    public sealed record RenderedFrame(ImageView Image, IReadOnlyList<GpFramePart> SkippedParts);

    /// <summary>
    /// Draws GP frames onto RGBA images, compositing the parts in order (alpha blending, straight alpha).
    /// </summary>
    public sealed class GpRenderer
    {
        private static readonly Color ShadowColor = Color.FromArgb(0x99, 0, 0, 0);

        private readonly Palette _palette;

        public GpRenderer(Palette palette)
        {
            _palette = palette;
        }

        /// <summary>Color used for <see cref="GpPartType.PlayerColor"/> parts.</summary>
        public NationColorOffset PlayerColor { get; set; } = NationColorOffset.Red;

        /// <summary>Canvas color under the sprite; transparent by default.</summary>
        public Color Background { get; set; } = Color.Transparent;

        /// <summary>
        /// Renders a frame on a canvas that starts at (0, 0) of the frame's coordinate system,
        /// so all frames of an animation line up.
        /// </summary>
        public RenderedFrame Render(GpFile file, GpFrame frame)
        {
            return Render(file, frame, frame.Width, frame.Height);
        }

        /// <summary>
        /// Renders a frame on a canvas of at least the given size, e.g. the largest frame of the file,
        /// so all frames of an animation have the same size.
        /// </summary>
        public RenderedFrame Render(GpFile file, GpFrame frame, int canvasWidth, int canvasHeight)
        {
            var image = new ImageView(
                Math.Max(Math.Max(frame.Width, canvasWidth), 1),
                Math.Max(Math.Max(frame.Height, canvasHeight), 1));
            Array.Fill(image.Pixels, Background);

            var skipped = new List<GpFramePart>();

            foreach (var part in frame.Parts)
            {
                if (!part.IsSupported)
                {
                    skipped.Add(part);
                    continue;
                }

                DrawPart(image, file, part);
            }

            return new RenderedFrame(image, skipped);
        }

        private void DrawPart(ImageView image, GpFile file, GpFramePart part)
        {
            var rows = part.DecodeRows();
            var pixels = part.DecodePixels(file.Dictionary);
            var alpha = AlphaOf(part.Type);
            var next = 0;

            for (var row = 0; row < rows.Count; row++)
            {
                var y = part.Y + row;
                var x = part.X;

                foreach (var run in rows[row])
                {
                    x += run.Skip;

                    for (var i = 0; i < run.Length; i++, x++)
                    {
                        Color color;
                        if (part.Type == GpPartType.Shadow)
                        {
                            color = ShadowColor;
                        }
                        else if (next < pixels.Length)
                        {
                            var index = pixels[next++];
                            color = part.Type == GpPartType.PlayerColor ? _palette[PlayerColor.Offset + index] : _palette[index];
                            color = Color.FromArgb(alpha, color);
                        }
                        else
                        {
                            // A few files describe more pixels in the shape than in the color data.
                            continue;
                        }

                        if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                        {
                            var target = y * image.Width + x;
                            image.Pixels[target] = Blend(color, image.Pixels[target]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Opacity per part type. The values for the blend and translucent types are estimates:
        /// edge pixels of type 2 lie furthest from the opaque body, type 4 closest.
        /// </summary>
        private static int AlphaOf(GpPartType type)
        {
            switch (type)
            {
                case GpPartType.Blend25: return 0x40;
                case GpPartType.Blend50: return 0x80;
                case GpPartType.Blend75: return 0xC0;
                case GpPartType.Translucent: return 0x80;
                default: return 0xFF;
            }
        }

        /// <summary>Source-over compositing with straight (non-premultiplied) alpha.</summary>
        private static Color Blend(Color source, Color destination)
        {
            if (source.A == 255 || destination.A == 0)
            {
                return source;
            }

            var sourceAlpha = source.A / 255.0;
            var destinationAlpha = destination.A / 255.0 * (1 - sourceAlpha);
            var outAlpha = sourceAlpha + destinationAlpha;

            int Channel(int s, int d) => (int) Math.Round((s * sourceAlpha + d * destinationAlpha) / outAlpha);

            return Color.FromArgb(
                (int) Math.Round(outAlpha * 255),
                Channel(source.R, destination.R),
                Channel(source.G, destination.G),
                Channel(source.B, destination.B));
        }
    }
}
