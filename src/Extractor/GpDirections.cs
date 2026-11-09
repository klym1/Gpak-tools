using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GPExtractor
{
    public enum GpDirectionsSource
    {
        /// <summary>Taken from a descriptor (<c>*.MD</c>) that uses the sprite.</summary>
        Descriptor,

        /// <summary>Recognized from the frames alone.</summary>
        FrameAnalysis,
    }

    /// <summary>
    /// How the frames of a unit sprite are split into directions and poses.
    /// </summary>
    /// <remarks>
    /// Frame <c>pose * StoredDirections + stored direction</c> shows a pose from one direction.
    /// With 2^n + 1 stored directions, they cover half a turn, from facing the viewer (south) clockwise
    /// over west to facing away (north); the eastern half is drawn by mirroring them around <see cref="AnchorX"/>.
    /// Directions are numbered clockwise from south: 0 = S, then towards W, N and E.
    /// </remarks>
    public sealed class GpDirections
    {
        private static readonly int[] MirroredCounts = { 3, 5, 9, 17, 33, 65, 129 };
        private static readonly string[] CompassNames =
        {
            "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW", "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
        };

        public GpDirections(int storedDirections, int poses, int anchorX, GpDirectionsSource source, string descriptorName = null)
        {
            if (storedDirections < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(storedDirections), "A sprite with directions stores at least 2.");
            }

            StoredDirections = storedDirections;
            Poses = poses;
            AnchorX = anchorX;
            Source = source;
            DescriptorName = descriptorName;
            IsMirrored = Array.IndexOf(MirroredCounts, storedDirections) >= 0;
            Count = IsMirrored ? 2 * (storedDirections - 1) : storedDirections;
        }

        /// <summary>Directions stored per pose.</summary>
        public int StoredDirections { get; }

        /// <summary>Directions available, including mirrored ones.</summary>
        public int Count { get; }

        /// <summary>Whether the eastern half is drawn by mirroring.</summary>
        public bool IsMirrored { get; }

        /// <summary>Number of poses (animation frames) per direction.</summary>
        public int Poses { get; }

        /// <summary>X coordinate mirrored frames are flipped around.</summary>
        public int AnchorX { get; }

        public GpDirectionsSource Source { get; }

        /// <summary>Name of the descriptor the layout comes from, if any.</summary>
        public string DescriptorName { get; }

        /// <summary>Returns which stored frame shows a pose from a direction, and whether it must be mirrored.</summary>
        public (int Frame, bool Mirrored) FrameOf(int pose, int direction)
        {
            if (pose < 0 || pose >= Poses)
            {
                throw new ArgumentOutOfRangeException(nameof(pose));
            }

            if (direction < 0 || direction >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(direction));
            }

            var mirrored = direction >= StoredDirections;
            var stored = mirrored ? Count - direction : direction;
            return (pose * StoredDirections + stored, mirrored);
        }

        /// <summary>Direction in degrees, clockwise from south.</summary>
        public double AngleOf(int direction) => 360.0 * direction / Count;

        /// <summary>A short name: a compass point for up to 16 directions, otherwise degrees.</summary>
        public string NameOf(int direction)
        {
            if (Count <= 16 && 16 % Count == 0)
            {
                return CompassNames[direction * (16 / Count)];
            }

            return AngleOf(direction).ToString("0.#", CultureInfo.InvariantCulture) + "°";
        }

        /// <summary>
        /// Finds the directions of a sprite: from a descriptor in the sprite's folder, its parent or its
        /// subfolders if one uses it, otherwise from the frames. Returns null for sprites without directions
        /// (buildings, effects, interface graphics).
        /// </summary>
        public static GpDirections Detect(string spritePath, GpFile file)
        {
            return FromDescriptors(Path.GetFileNameWithoutExtension(spritePath), file, FindDescriptors(spritePath))
                   ?? FromFrames(file);
        }

        /// <summary>Uses the descriptors that refer to the sprite by name.</summary>
        public static GpDirections FromDescriptors(string spriteName, GpFile file, IEnumerable<GpDescriptor> descriptors)
        {
            var frameCount = file.Frames.Count;

            var uses = descriptors
                .SelectMany(descriptor => descriptor.Sprites
                    .Where(sprite => sprite.FileName.Equals(spriteName, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(sprite => descriptor.Animations
                        .Where(animation => animation.Frames.Any(frame => frame.Sprite == sprite.Index))
                        .Select(animation => (Descriptor: descriptor, Sprite: sprite, animation.Directions))))
                .ToList();

            // A sprite can be shared by several descriptors; take the most common layout that fits the frames.
            var best = uses
                .Where(use => use.Directions > 1 && frameCount % use.Directions == 0)
                .GroupBy(use => use.Directions)
                .OrderByDescending(group => group.Count())
                .FirstOrDefault();

            if (best == null)
            {
                return null;
            }

            var directions = best.Key;
            var source = best.FirstOrDefault(use => use.Sprite.AnchorX.HasValue);
            if (source.Descriptor == null)
            {
                source = best.First();
            }

            var anchorX = source.Sprite.AnchorX ?? EstimateAnchorX(file, directions);
            return new GpDirections(directions, frameCount / directions, anchorX, GpDirectionsSource.Descriptor, source.Descriptor.Name);
        }

        /// <summary>
        /// Recognizes 9, 17, 33, 65 or 129 directions by comparing frame outlines: frames one pose apart in the
        /// same direction look alike, frames of neighbouring directions do not.
        /// </summary>
        public static GpDirections FromFrames(GpFile file)
        {
            // Interface graphics (menus, pictures, fonts) never have directions.
            if (file.Frames.Any(frame => frame.Parts.Any(part => part.Type == GpPartType.Uncompressed)))
            {
                return null;
            }

            var frameCount = file.Frames.Count;
            var bounds = file.Frames.Select(Bounds).ToArray();

            GpDirections best = null;
            var bestScore = double.MaxValue;

            foreach (var directions in new[] { 9, 17, 33, 65, 129 })
            {
                if (frameCount % directions != 0)
                {
                    continue;
                }

                if (frameCount == directions)
                {
                    // One pose seen from many directions (ships, cannons): too few frames to compare,
                    // but 17 or more frames in exactly such a count are a strong hint.
                    if (directions >= 17 && best == null)
                    {
                        best = new GpDirections(directions, 1, EstimateAnchorX(file, directions), GpDirectionsSource.FrameAnalysis);
                    }

                    continue;
                }

                var score = Score(bounds, directions);
                if (score < 1.0 && score < bestScore)
                {
                    bestScore = score;
                    best = new GpDirections(directions, frameCount / directions, EstimateAnchorX(file, directions), GpDirectionsSource.FrameAnalysis);
                }
            }

            return best;
        }

        /// <summary>
        /// Searches the sprite's folder, its parent and their direct subfolders for descriptors.
        /// </summary>
        public static IReadOnlyList<GpDescriptor> FindDescriptors(string spritePath)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(spritePath));
            var folders = new List<string> { folder };
            var parent = Path.GetDirectoryName(folder);
            if (parent != null)
            {
                folders.Add(parent);
            }

            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in folders.ToList())
            {
                try
                {
                    folders.AddRange(Directory.EnumerateDirectories(candidate));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                }
            }

            foreach (var candidate in folders)
            {
                try
                {
                    foreach (var path in Directory.EnumerateFiles(candidate))
                    {
                        if (string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase))
                        {
                            files.Add(Path.GetFullPath(path));
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                }
            }

            var descriptors = new List<GpDescriptor>();
            foreach (var path in files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    descriptors.Add(GpDescriptor.Load(path));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                }
            }

            return descriptors;
        }

        private readonly record struct Box(int Left, int Right, int Top, int Bottom);

        private static Box Bounds(GpFrame frame)
        {
            if (frame.Parts.Count == 0)
            {
                return default;
            }

            return new Box(
                frame.Parts.Min(part => part.X),
                frame.Parts.Max(part => part.X + part.Width),
                frame.Parts.Min(part => part.Y),
                frame.Parts.Max(part => part.Y + part.Height));
        }

        private static int Distance(Box a, Box b) =>
            Math.Abs(a.Left - b.Left) + Math.Abs(a.Right - b.Right) + Math.Abs(a.Top - b.Top) + Math.Abs(a.Bottom - b.Bottom);

        /// <summary>Average outline change between poses of one direction, relative to that between directions.</summary>
        private static double Score(Box[] bounds, int directions)
        {
            double samePose = 0, sameDirection = 0;
            int samePoseCount = 0, sameDirectionCount = 0;

            for (var i = 0; i + 1 < bounds.Length; i++)
            {
                if ((i + 1) % directions != 0)
                {
                    samePose += Distance(bounds[i], bounds[i + 1]);
                    samePoseCount++;
                }

                if (i + directions < bounds.Length)
                {
                    sameDirection += Distance(bounds[i], bounds[i + directions]);
                    sameDirectionCount++;
                }
            }

            if (samePoseCount == 0 || sameDirectionCount == 0)
            {
                return double.MaxValue;
            }

            return (sameDirection / sameDirectionCount + 1) / (samePose / samePoseCount + 1);
        }

        /// <summary>
        /// The first pose seen from the front (direction 0) and from behind (the last stored direction) is
        /// symmetric around the anchor; later poses (e.g. lying after death) need not be.
        /// </summary>
        private static int EstimateAnchorX(GpFile file, int directions)
        {
            var centres = new[] { 0, directions - 1 }
                .Where(frame => frame < file.Frames.Count)
                .Select(frame => Bounds(file.Frames[frame]))
                .Where(box => box.Right > box.Left)
                .Select(box => (box.Left + box.Right) / 2.0)
                .ToList();

            return centres.Count == 0 ? 0 : (int) Math.Round(centres.Average());
        }
    }
}
