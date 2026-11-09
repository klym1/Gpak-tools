using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GPExtractor
{
    /// <summary>
    /// The parts of a game object descriptor (<c>*.MD</c>, extracted from the game's archive) that describe its sprites.
    /// </summary>
    /// <remarks>
    /// Relevant lines:
    /// <code>
    /// USERLC 1 ba1g  SHADOW -62 -90             sprite 1 is ba1g.gp; the object's anchor is 62 pixels from the left
    /// #MOTION_L  9 10  1 0 1 1 ... 1 9          animation: 9 stored directions, 10 poses as (sprite, pose) pairs
    /// </code>
    /// Frame <c>pose * directions + direction</c> of the sprite shows that pose from that direction.
    /// </remarks>
    public sealed class GpDescriptor
    {
        private GpDescriptor(string name, IReadOnlyList<GpDescriptorSprite> sprites, IReadOnlyList<GpDescriptorAnimation> animations)
        {
            Name = name;
            Sprites = sprites;
            Animations = animations;
        }

        /// <summary>File name without extension.</summary>
        public string Name { get; }

        public IReadOnlyList<GpDescriptorSprite> Sprites { get; }

        public IReadOnlyList<GpDescriptorAnimation> Animations { get; }

        public static GpDescriptor Load(string path)
        {
            // The game files are in a Windows code page; only ASCII keywords and names matter here.
            return Parse(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path, Encoding.Latin1));
        }

        public static GpDescriptor Parse(string name, string text)
        {
            var sprites = new List<GpDescriptorSprite>();
            var animations = new List<GpDescriptorAnimation>();

            foreach (var line in text.Split('\n'))
            {
                var tokens = line.Split((char[]) null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 3)
                {
                    continue;
                }

                if (tokens[0].Equals("USERLC", StringComparison.OrdinalIgnoreCase) && TryInt(tokens[1], out var index))
                {
                    int? anchorX = null;
                    if (tokens.Length >= 5 && tokens[3].Equals("SHADOW", StringComparison.OrdinalIgnoreCase) && TryInt(tokens[4], out var shadowX))
                    {
                        anchorX = -shadowX;
                    }

                    sprites.Add(new GpDescriptorSprite(index, tokens[2], anchorX));
                }
                else if (tokens[0].StartsWith('#') && TryInt(tokens[1], out var directions) && TryInt(tokens[2], out var count) && directions > 0)
                {
                    var frames = new List<(int Sprite, int Pose)>();
                    for (var i = 0; i < count && 4 + 2 * i < tokens.Length; i++)
                    {
                        if (TryInt(tokens[3 + 2 * i], out var sprite) && TryInt(tokens[4 + 2 * i], out var pose))
                        {
                            frames.Add((sprite, pose));
                        }
                    }

                    animations.Add(new GpDescriptorAnimation(tokens[0].Substring(1), directions, frames));
                }
            }

            return new GpDescriptor(name, sprites, animations);
        }

        private static bool TryInt(string token, out int value)
        {
            return int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }
    }

    /// <param name="Index">Number the animations use to refer to this sprite.</param>
    /// <param name="FileName">Sprite file name without extension (e.g. <c>ba1g</c>).</param>
    /// <param name="AnchorX">X coordinate of the object's anchor in the sprite, the axis mirrored directions are flipped around.</param>
    public sealed record GpDescriptorSprite(int Index, string FileName, int? AnchorX);

    /// <param name="Name">e.g. <c>MOTION_L</c>.</param>
    /// <param name="Directions">Number of directions stored in the sprite for each pose.</param>
    /// <param name="Frames">The animation's frames as (sprite index, pose) pairs.</param>
    public sealed record GpDescriptorAnimation(string Name, int Directions, IReadOnlyList<(int Sprite, int Pose)> Frames);
}
