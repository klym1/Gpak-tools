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
        private const int PlayerRampLength = 4;

        private static readonly Color ShadowColor = Color.FromArgb(0x99, 0, 0, 0);

        private readonly Palette _palette;

        public GpRenderer(Palette palette)
        {
            _palette = palette;
        }

        /// <summary>Color used for <see cref="GpPartType.PlayerColor"/> parts.</summary>
        public NationColorOffset PlayerColor { get; set; } = NationColorOffset.Red;

        /// <summary>
        /// Draws the sprite as the game draws smoke: every pixel in this one color, the more opaque the
        /// brighter the pixel is, in the 32 steps of the game's blend tables. Null draws the sprite as it is.
        /// </summary>
        public Color? Fog { get; set; }

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
        /// <param name="offsetX">Moves the frame right by this many pixels.</param>
        /// <param name="offsetY">Moves the frame down by this many pixels.</param>
        /// <param name="mirror">
        /// Flips the result horizontally around the canvas centre, for directions the game draws mirrored.
        /// Place the frame's anchor at the centre with <paramref name="offsetX"/> to flip around the anchor.
        /// </param>
        public RenderedFrame Render(GpFile file, GpFrame frame, int canvasWidth, int canvasHeight, int offsetX = 0, bool mirror = false, int offsetY = 0)
        {
            var image = new ImageView(
                Math.Max(Math.Max(frame.Width + offsetX, canvasWidth), 1),
                Math.Max(Math.Max(frame.Height + offsetY, canvasHeight), 1));
            Array.Fill(image.Pixels, Background);

            var skipped = new List<GpFramePart>();

            foreach (var part in frame.Parts)
            {
                if (!part.IsSupported)
                {
                    skipped.Add(part);
                    continue;
                }

                DrawPart(image, file, part, offsetX, offsetY, mirror);
            }

            return new RenderedFrame(image, skipped);
        }

        /// <summary>
        /// Renders an RLC frame like a GP frame. Its pixels in the red player ramp are drawn in
        /// <see cref="PlayerColor"/>.
        /// </summary>
        public RenderedFrame Render(
              RlcFrame frame
            , int canvasWidth
            , int canvasHeight
            , int offsetX = 0
            , bool mirror = false
            , int offsetY = 0
        )
        {
            var image = new ImageView(
                Math.Max(Math.Max(frame.Width + offsetX, canvasWidth), 1),
                Math.Max(Math.Max(frame.Height + offsetY, canvasHeight), 1));
            Array.Fill(image.Pixels, Background);

            var red = NationColorOffset.Red.Offset;
            for (var row = 0; row < frame.Rows.Count; row++)
            {
                var y = row + offsetY;
                foreach (var run in frame.Rows[row])
                {
                    for (var i = 0; i < run.Pixels.Length; i++)
                    {
                        var x = run.X + offsetX + i;
                        var targetX = mirror ? image.Width - 1 - x : x;
                        if (targetX < 0 || targetX >= image.Width || y < 0 || y >= image.Height)
                        {
                            continue;
                        }

                        int index = run.Pixels[i];
                        if (index >= red && index < red + PlayerRampLength)
                        {
                            index += PlayerColor.Offset - red;
                        }

                        image.Pixels[y * image.Width + targetX] = _palette[index];
                    }
                }
            }

            return new RenderedFrame(image, Array.Empty<GpFramePart>());
        }

        // The grays of the 20 brightness levels of the game's light table (ALPHAW.GRD).
        private static readonly int[] LightLevels =
        {
            0, 20, 28, 40, 52, 64, 76, 88, 88, 100, 108, 120, 132, 144, 156, 168, 180, 192, 204, 216,
        };

        private const int LightSteps = 8;

        /// <summary>
        /// A pixel of a translucent part (smoke, explosions, splashes) is not a palette color but a row of the
        /// light table: brightness level × 8 + step. The game adds the level's gray to what is behind, dimmed
        /// to (7 - step) / 8: step 0 is a faint glow, step 7 solid gray.
        /// </summary>
        private static Color LightOf(int value)
        {
            var gray = LightLevels[Math.Min(value / LightSteps, LightLevels.Length - 1)];
            var opacity = (value % LightSteps + 1) / (double) LightSteps;

            // The gray is what is added, so the color to lay over at this opacity is brighter.
            var shade = (int) Math.Min(255, Math.Round(gray / opacity));
            return Color.FromArgb(
                  (int) Math.Round(255 * opacity)
                , shade
                , shade
                , shade
            );
        }

        private const int FogLevels = 32;

        /// <summary>The strength of a pixel laid over the picture as fog: its brightness in 32 steps.</summary>
        private static int FogLevelOf(Color color) => Math.Max(color.R, Math.Max(color.G, color.B)) * FogLevels / 256;

        private void DrawPart(ImageView image, GpFile file, GpFramePart part, int offsetX, int offsetY, bool mirror)
        {
            var rows = part.DecodeRows();
            var pixels = part.DecodePixels(file.Dictionary);
            var alpha = AlphaOf(part.Type);
            var next = 0;

            for (var row = 0; row < rows.Count; row++)
            {
                var y = part.Y + offsetY + row;
                var x = part.X + offsetX;

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
                            if (part.Type == GpPartType.Translucent)
                            {
                                color = LightOf(index);
                            }
                            else
                            {
                                color = part.Type == GpPartType.PlayerColor ? _palette[PlayerColor.Offset + index] : _palette[index];
                                color = Fog is { } fog
                                    ? Color.FromArgb(FogLevelOf(color) * 255 / (FogLevels - 1), fog)
                                    : Color.FromArgb(alpha, color);
                            }
                        }
                        else
                        {
                            // A few files describe more pixels in the shape than in the color data.
                            continue;
                        }

                        var targetX = mirror ? image.Width - 1 - x : x;
                        if (targetX >= 0 && targetX < image.Width && y >= 0 && y < image.Height)
                        {
                            var target = y * image.Width + targetX;
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
