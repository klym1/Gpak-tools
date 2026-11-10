using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GPExtractor
{
    /// <summary>An animation of a game object, e.g. walking or dying.</summary>
    /// <param name="Code">The descriptor's animation name, e.g. <c>MOTION_L</c>; several joined with <c>+</c>.</param>
    /// <param name="Label">Readable name, with the code.</param>
    /// <param name="Directions">Directions stored per pose in the sprites (1 for objects without directions).</param>
    /// <param name="Frames">The frames in playing order, as (sprite index, pose) pairs.</param>
    public sealed record GameObjectState(string Code, string Label, int Directions, IReadOnlyList<(int Sprite, int Pose)> Frames);

    /// <summary>A unit, ship or building as described by one descriptor.</summary>
    public sealed class GameObject
    {
        internal GameObject(GpDescriptor descriptor, string path, IReadOnlyList<GameObjectState> states)
        {
            Descriptor = descriptor;
            Path = path;
            States = states;
        }

        public GpDescriptor Descriptor { get; }

        /// <summary>Path of the descriptor in the game files.</summary>
        public string Path { get; }

        public IReadOnlyList<GameObjectState> States { get; }

        /// <summary>Whether it has directions: units, ships, cannons.</summary>
        public bool IsUnit => Descriptor.HasDirections;

        /// <summary>Display name with the internal name, e.g. "Барабанщик 17в (BA1)".</summary>
        public string Label => (Descriptor.DisplayName ?? Descriptor.Name) + " (" + Descriptor.FileName + ")";

        public GpDescriptorSprite Sprite(int index) => Descriptor.Sprites.FirstOrDefault(sprite => sprite.Index == index);

        public override string ToString() => Label;
    }

    /// <summary>
    /// The game objects described in the game files, and their sprites.
    /// </summary>
    public sealed class GameCatalog : IDisposable
    {
        private static readonly Dictionary<string, string> KnownStates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["STAND"] = "Stand",
            ["MOTION_L"] = "Walk, left step",
            ["MOTION_R"] = "Walk, right step",
            ["MOTION_L+MOTION_R"] = "Walk",
            ["MOTION_LB"] = "Walk backwards, left step",
            ["MOTION_RB"] = "Walk backwards, right step",
            ["MOTION_LB+MOTION_RB"] = "Walk backwards",
            ["ATTACK"] = "Attack",
            ["DEATH"] = "Death",
            ["REST"] = "Rest",
            ["WORK"] = "Work",
        };

        private readonly ConcurrentDictionary<string, GpFile> _sprites = new ConcurrentDictionary<string, GpFile>(StringComparer.OrdinalIgnoreCase);

        private GameCatalog(GameFiles files, IReadOnlyList<GameObject> objects)
        {
            Files = files;
            Objects = objects;
        }

        public GameFiles Files { get; }

        /// <summary>All objects with at least one animation, units first, then by name.</summary>
        public IReadOnlyList<GameObject> Objects { get; }

        public IEnumerable<GameObject> Units => Objects.Where(gameObject => gameObject.IsUnit);

        /// <summary>Reads every descriptor (<c>*.MD</c>) in the game files.</summary>
        public static GameCatalog Load(GameFiles files)
        {
            var objects = new List<GameObject>();

            foreach (var path in files.Paths.Where(path => path.EndsWith(".MD", StringComparison.OrdinalIgnoreCase)))
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path.Substring(path.LastIndexOf('\\') + 1));
                var descriptor = GpDescriptor.Parse(fileName, files.Read(path));
                var states = StatesOf(descriptor);

                if (states.Count > 0)
                {
                    objects.Add(new GameObject(descriptor, path, states));
                }
            }

            var ordered = objects
                .OrderByDescending(gameObject => gameObject.IsUnit)
                .ThenBy(gameObject => gameObject.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return new GameCatalog(files, ordered);
        }

        /// <summary>
        /// The sprite a descriptor refers to by file name (without extension), or null if the game files
        /// have no GP sprite of that name (some use another format, RLC).
        /// </summary>
        public GpFile Sprite(string fileName)
        {
            return _sprites.GetOrAdd(fileName, name =>
            {
                var path = Files.Find(name, ".gp");
                if (path == null)
                {
                    return null;
                }

                try
                {
                    return GpFile.Parse(Files.Read(path));
                }
                catch (InvalidDataException)
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// The descriptor's animations, plus "Walk" (and "Walk backwards") joining the left and right steps.
        /// </summary>
        public static IReadOnlyList<GameObjectState> StatesOf(GpDescriptor descriptor)
        {
            var states = descriptor.Animations
                .Where(animation => animation.Frames.Count > 0)
                .GroupBy(animation => animation.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(animation => new GameObjectState(animation.Name, LabelOf(animation.Name), animation.Directions, animation.Frames))
                .ToList();

            Join(states, "MOTION_L", "MOTION_R");
            Join(states, "MOTION_LB", "MOTION_RB");
            return states;
        }

        /// <summary>Adds a state that plays two others in turn (the left and right step of a walk).</summary>
        private static void Join(List<GameObjectState> states, string firstCode, string secondCode)
        {
            var first = states.FirstOrDefault(state => state.Code.Equals(firstCode, StringComparison.OrdinalIgnoreCase));
            var second = states.FirstOrDefault(state => state.Code.Equals(secondCode, StringComparison.OrdinalIgnoreCase));
            if (first != null && second != null && first.Directions == second.Directions)
            {
                var code = firstCode + "+" + secondCode;
                states.Insert(states.IndexOf(first), new GameObjectState(code, LabelOf(code), first.Directions, first.Frames.Concat(second.Frames).ToList()));
            }
        }

        private static string LabelOf(string code)
        {
            if (KnownStates.TryGetValue(code, out var label))
            {
                return label + " (" + code + ")";
            }

            // DEATHLIE1, DEATHLIE2, ...: the poses of a dead unit.
            if (code.StartsWith("DEATHLIE", StringComparison.OrdinalIgnoreCase))
            {
                return "Lying dead " + code.Substring("DEATHLIE".Length) + " (" + code + ")";
            }

            return code;
        }

        public void Dispose()
        {
            Files.Dispose();
        }
    }
}
