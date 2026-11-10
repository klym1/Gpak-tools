using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GPExtractor
{
    /// <summary>
    /// The parts of a game object descriptor (<c>*.MD</c>, in the game's archive) that describe how it looks.
    /// </summary>
    /// <remarks>
    /// Relevant lines:
    /// <code>
    /// NAME Barabanshik_1                         internal name
    /// MESSAGE ...                                display name, in a Cyrillic code page
    /// USERLC 1 ba1g  SHADOW -62 -90             sprite 1 is ba1g.gp; the object's anchor is at (62, 90) in it
    /// #MOTION_L  9 10  1 0 1 1 ... 1 9          animation: 9 stored directions, 10 frames as (sprite, pose) pairs
    /// @MOTION_LB 9 1 19 10                      the same as a range: sprite 1, poses 19 down to 11
    /// </code>
    /// Frame <c>pose * directions + direction</c> of a sprite shows that pose from that direction.
    /// </remarks>
    public sealed class GpDescriptor
    {
        // Only ASCII whitespace: bytes like 0x85 and 0xA0 are Cyrillic letters in the game's code pages.
        private static readonly char[] Whitespace = { (char) 32, (char) 9, (char) 13 }; // space, tab, CR

        private static readonly Lazy<Encoding[]> CyrillicEncodings = new Lazy<Encoding[]>(() =>
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return new[] { Encoding.GetEncoding(866), Encoding.GetEncoding(1251) };
        });

        private GpDescriptor(string fileName, string name, string displayName,
            IReadOnlyList<GpDescriptorSprite> sprites, IReadOnlyList<GpDescriptorAnimation> animations)
        {
            FileName = fileName;
            Name = name;
            DisplayName = displayName;
            Sprites = sprites;
            Animations = animations;
        }

        /// <summary>Descriptor file name without extension, e.g. <c>BA1</c>.</summary>
        public string FileName { get; }

        /// <summary>The <c>NAME</c> line, or the file name if there is none.</summary>
        public string Name { get; }

        /// <summary>The <c>MESSAGE</c> line decoded (underscores as spaces), or null.</summary>
        public string DisplayName { get; }

        public IReadOnlyList<GpDescriptorSprite> Sprites { get; }

        public IReadOnlyList<GpDescriptorAnimation> Animations { get; }

        /// <summary>Whether any animation has several directions: units, ships, cannons.</summary>
        public bool HasDirections => Animations.Any(animation => animation.Directions > 1);

        public static GpDescriptor Load(string path)
        {
            return Parse(Path.GetFileNameWithoutExtension(path), File.ReadAllBytes(path));
        }

        public static GpDescriptor Parse(string fileName, byte[] bytes)
        {
            // Latin-1 maps each byte to one char, so the MESSAGE bytes can be recovered for decoding.
            return Parse(fileName, Encoding.Latin1.GetString(bytes));
        }

        /// <param name="text">The descriptor, one char per byte (Latin-1).</param>
        public static GpDescriptor Parse(string fileName, string text)
        {
            string name = null;
            string displayName = null;
            var sprites = new List<GpDescriptorSprite>();
            var animations = new List<GpDescriptorAnimation>();

            foreach (var line in text.Split('\n'))
            {
                var tokens = line.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2)
                {
                    continue;
                }

                if (tokens[0] == "NAME" && name == null)
                {
                    name = tokens[1];
                }
                else if (tokens[0] == "MESSAGE" && displayName == null)
                {
                    displayName = DecodeCyrillic(Encoding.Latin1.GetBytes(line.Trim(Whitespace).Substring("MESSAGE".Length).Trim(Whitespace)))
                        .Replace('_', ' ');
                }
                else if (tokens.Length >= 3 && tokens[0].Equals("USERLC", StringComparison.OrdinalIgnoreCase) && TryInt(tokens[1], out var index))
                {
                    int? anchorX = null, anchorY = null;
                    if (tokens.Length >= 6 && tokens[3].Equals("SHADOW", StringComparison.OrdinalIgnoreCase)
                        && TryInt(tokens[4], out var shadowX) && TryInt(tokens[5], out var shadowY))
                    {
                        anchorX = -shadowX;
                        anchorY = -shadowY;
                    }

                    sprites.Add(new GpDescriptorSprite(index, tokens[2], anchorX, anchorY));
                }
                else if (tokens.Length >= 3 && tokens[0].StartsWith('#') && TryInt(tokens[1], out var directions)
                         && TryInt(tokens[2], out var count) && directions > 0)
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
                else if (tokens.Length >= 5 && tokens[0].StartsWith('@') && TryInt(tokens[1], out var rangeDirections)
                         && TryInt(tokens[2], out var rangeSprite) && TryInt(tokens[3], out var from) && TryInt(tokens[4], out var to)
                         && rangeDirections > 0)
                {
                    animations.Add(new GpDescriptorAnimation(tokens[0].Substring(1), rangeDirections, Range(rangeSprite, from, to)));
                }
            }

            return new GpDescriptor(fileName, name ?? fileName, displayName, sprites, animations);
        }

        /// <summary>
        /// The poses of a compact animation line <c>@NAME directions sprite from to</c>: from <c>from</c> towards
        /// <c>to</c>, which is not included (<c>@DEATH 9 3 0 25</c> plays poses 0-24, and the corpse lies in pose 25).
        /// A descending range plays backwards, e.g. walking backwards.
        /// </summary>
        private static IReadOnlyList<(int Sprite, int Pose)> Range(int sprite, int from, int to)
        {
            if (from == to)
            {
                return new[] { (sprite, from) };
            }

            var step = from < to ? 1 : -1;
            var frames = new List<(int Sprite, int Pose)>();
            for (var pose = from; pose != to; pose += step)
            {
                frames.Add((sprite, pose));
            }

            return frames;
        }

        /// <summary>
        /// The game stores names in DOS (866) or Windows (1251) Cyrillic; picks the one that reads as words.
        /// </summary>
        private static string DecodeCyrillic(byte[] bytes)
        {
            return CyrillicEncodings.Value
                .Select(encoding => encoding.GetString(bytes))
                .OrderByDescending(text => text.Count(c => char.IsLetterOrDigit(c) || c == '_' || c == '.'))
                .First();
        }

        private static bool TryInt(string token, out int value)
        {
            return int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }
    }

    /// <param name="Index">Number the animations use to refer to this sprite.</param>
    /// <param name="FileName">Sprite file name without extension (e.g. <c>ba1g</c>).</param>
    /// <param name="AnchorX">X of the object's anchor in the sprite, the axis mirrored directions are flipped around.</param>
    /// <param name="AnchorY">Y of the object's anchor in the sprite.</param>
    public sealed record GpDescriptorSprite(int Index, string FileName, int? AnchorX, int? AnchorY = null);

    /// <param name="Name">e.g. <c>MOTION_L</c>.</param>
    /// <param name="Directions">Number of directions stored in the sprite for each pose.</param>
    /// <param name="Frames">The animation's frames as (sprite index, pose) pairs.</param>
    public sealed record GpDescriptorAnimation(string Name, int Directions, IReadOnlyList<(int Sprite, int Pose)> Frames);
}
