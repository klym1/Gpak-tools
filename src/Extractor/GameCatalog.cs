using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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

        /// <summary>
        /// Whether it is a building: an object without directions that stands (<c>STANDLO</c>). The frames of
        /// such a state are not shown in turn but together, as layers: the ground with the shadow, then the walls.
        /// </summary>
        public bool IsBuilding => !IsUnit && States.Any(state => state.Code.StartsWith("STANDLO", StringComparison.OrdinalIgnoreCase));

        /// <summary>Display name with the internal name, e.g. "Барабанщик 17в (BA1)".</summary>
        public string Label => (Descriptor.DisplayName ?? Descriptor.Name) + " (" + Descriptor.FileName + ")";

        public GpDescriptorSprite Sprite(int index) => Descriptor.Sprites.FirstOrDefault(sprite => sprite.Index == index);

        public override string ToString() => Label;
    }

    /// <summary>
    /// The game objects described in the game files, and their sprites.
    /// </summary>
    /// <summary>A sound an object plays at a frame of an animation (e.g. <c>DEATH</c>).</summary>
    /// <param name="Impact">Whether it is the sound of a weapon landing, some time after the frame it is fired at.</param>
    public sealed record GameObjectSound(string Animation, int Frame, GameSound Sound, bool Impact = false);

    /// <summary>A weapon fired at a frame of a state, from one of the points (see <see cref="GameCatalog.ShotsOf"/>).</summary>
    public sealed record GameObjectShot(int Frame, string Weapon, IReadOnlyList<(int X, int Y)> Points);

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
        private static readonly Dictionary<string, string> BlendTableFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WHITE"] = "AGEW_1W",
            ["ALPHAGB"] = "AGEW_1W",
            ["DARK"] = "AGEW_1D",
            ["RED"] = "AGEW_1R",
            ["BRIGHT"] = "AGEW_1BR",
        };

        private readonly ConcurrentDictionary<string, GrdTable> _blendTables = new ConcurrentDictionary<string, GrdTable>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, GameObject> _effects = new ConcurrentDictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, RlcFile> _rlcSprites = new ConcurrentDictionary<string, RlcFile>(StringComparer.OrdinalIgnoreCase);

        private GameCatalog(GameFiles files, IReadOnlyList<GameObject> objects)
        {
            Files = files;
            Objects = objects;

            var soundList = files.Find("SOUNDLIST", ".TXT");
            Sounds = soundList == null ? SoundList.Empty : SoundList.Parse(Encoding.Latin1.GetString(files.Read(soundList)));

            var weaponList = files.Find("WEAPON", ".NDS");
            Weapons = weaponList == null ? WeaponList.Empty : WeaponList.Parse(Encoding.Latin1.GetString(files.Read(weaponList)));

            var weaponAnimations = files.Find("WEAPON", ".ADS");
            WeaponAnimations = weaponAnimations == null ? null : GpDescriptor.Parse("WEAPON", files.Read(weaponAnimations));
        }

        /// <summary>
        /// The animations of weapons (smoke, explosions), from <c>WEAPON.ADS</c>, which is written like a
        /// descriptor; null if the game files have none.
        /// </summary>
        public GpDescriptor WeaponAnimations { get; }

        /// <summary>
        /// A weapon animation (e.g. <c>DIM32</c>, the smoke of a cannon) as an object with that one state, so
        /// it can be drawn like a unit; null if there is no such animation.
        /// </summary>
        public GameObject Effect(string animation)
        {
            return _effects.GetOrAdd(animation, name =>
            {
                var state = WeaponAnimations == null
                    ? null
                    : StatesOf(WeaponAnimations).FirstOrDefault(s => s.Code.Equals(name, StringComparison.OrdinalIgnoreCase) && s.Frames.Count > 0);

                return state == null ? null : new GameObject(WeaponAnimations, "WEAPON.ADS", new[] { state });
            });
        }

        /// <summary>
        /// The table (<c>*.GRD</c>) the game lays a weapon of a transparency over the picture with: of its 32
        /// rows, row n is what every palette color turns into under a pixel of strength n: all the way to one
        /// color in the last row (light gray for <c>WHITE</c>, smoke). <c>ALPHAGB</c>, the smoke of the mortar, is
        /// taken to be laid over like <c>WHITE</c>. Null for <c>NONE</c> and for kinds that are not known.
        /// </summary>
        public GrdTable BlendTable(string transparency)
        {
            return _blendTables.GetOrAdd(transparency ?? "", name =>
            {
                var path = BlendTableFiles.TryGetValue(name, out var file) ? Files.Find(file, ".GRD") : null;
                if (path == null)
                {
                    return null;
                }

                try
                {
                    return GrdTable.Parse(Files.Read(path));
                }
                catch (InvalidDataException)
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// The weapons a state fires: at which frame, and where the weapon is then (one point per stored
        /// direction, in the coordinates of the state's sprite; empty if the descriptor gives none).
        /// </summary>
        public IReadOnlyList<GameObjectShot> ShotsOf(GameObject gameObject, GameObjectState state)
        {
            var descriptor = gameObject.Descriptor;
            return descriptor.Weapons
                .Where(weapon => SameAnimation(weapon.Animation, state.Code))
                .Select(weapon => new GameObjectShot(
                      FiringFrameOf(descriptor, weapon.Animation)
                    , weapon.Weapon
                    , descriptor.ActivePoints
                        .Where(point => SameAnimation(point.Animation, weapon.Animation))
                        .Select(point => point.Points)
                        .FirstOrDefault() ?? Array.Empty<(int X, int Y)>()
                ))
                .ToList();
        }

        /// <summary>The frame an attack fires at, or its first frame if the descriptor gives none.</summary>
        private static int FiringFrameOf(GpDescriptor descriptor, string animation)
        {
            return descriptor.FiringFrames
                .Where(firing => SameAnimation(firing.Animation, animation))
                .Select(firing => firing.Frame)
                .FirstOrDefault();
        }

        /// <summary>The weapons of <c>WEAPON.NDS</c>; empty if the game files have none.</summary>
        public WeaponList Weapons { get; }

        /// <summary>The sounds of <c>SOUNDLIST.TXT</c>; empty if the game files have none.</summary>
        public SoundList Sounds { get; }

        /// <summary>
        /// The sounds of a state by its frame. In a state joined from several animations (<c>MOTION_L+MOTION_R</c>),
        /// the frames of the later ones follow those before.
        /// </summary>
        public IReadOnlyList<(int Frame, GameSound Sound, bool Impact)> SoundsOf(GameObject gameObject, GameObjectState state)
        {
            var sounds = SoundsOf(gameObject);
            var result = new List<(int Frame, GameSound Sound, bool Impact)>();
            var offset = 0;

            foreach (var code in state.Code.Split('+'))
            {
                result.AddRange(sounds
                    .Where(sound => SameAnimation(sound.Animation, code))
                    .Select(sound => (offset + sound.Frame, sound.Sound, sound.Impact)));

                offset += gameObject.Descriptor.Animations
                    .FirstOrDefault(animation => animation.Name.Equals(code, StringComparison.OrdinalIgnoreCase))
                    ?.Frames.Count ?? 0;
            }

            return result;
        }

        /// <summary>Whether two animation names mean the same one: the first attack is <c>ATTACK</c> or <c>ATTACK0</c>.</summary>
        public static bool SameAnimation(string first, string second)
        {
            static string Normalize(string name) => name.Equals("ATTACK0", StringComparison.OrdinalIgnoreCase) ? "ATTACK" : name;

            return Normalize(first).Equals(Normalize(second), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The sounds an object plays, with their wave files: at which frame of which animation. These are its
        /// <c>SOUND</c> lines and the shots and impacts of its weapons.
        /// </summary>
        public IReadOnlyList<GameObjectSound> SoundsOf(GameObject gameObject)
        {
            var descriptor = gameObject.Descriptor;

            // A weapon sounds when it fires.
            var shots = descriptor.Weapons.SelectMany(weapon =>
            {
                var frame = FiringFrameOf(descriptor, weapon.Animation);

                return new[]
                {
                    (weapon.Animation, Frame: frame, Sound: Weapons.ShotSoundOf(weapon.Weapon), Impact: false),
                    (weapon.Animation, Frame: frame, Sound: Weapons.ImpactSoundOf(weapon.Weapon), Impact: true),
                };
            });

            return descriptor.Sounds
                .Select(sound => (sound.Animation, sound.Frame, sound.Sound, Impact: false))
                .Concat(shots)
                .Where(sound => sound.Sound != null)
                .Select(sound => new GameObjectSound(
                      sound.Animation
                    , sound.Frame
                    , Sounds.Find(sound.Sound)
                    , sound.Impact
                ))
                .Where(sound => sound.Sound != null)
                .ToList();
        }

        public GameFiles Files { get; }

        /// <summary>All objects with at least one animation, units first, then by name.</summary>
        public IReadOnlyList<GameObject> Objects { get; }

        public IEnumerable<GameObject> Units => Objects.Where(gameObject => gameObject.IsUnit);

        public IEnumerable<GameObject> Buildings => Objects.Where(gameObject => gameObject.IsBuilding);

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
        /// have no GP sprite of that name (see <see cref="RlcSprite"/>).
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

        /// <summary>The RLC sprite of that name, or null if the game files have none.</summary>
        public RlcFile RlcSprite(string fileName)
        {
            return _rlcSprites.GetOrAdd(fileName, name =>
            {
                var path = Files.Find(name, ".rlc");
                if (path == null)
                {
                    return null;
                }

                try
                {
                    return RlcFile.Parse(Files.Read(path));
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
