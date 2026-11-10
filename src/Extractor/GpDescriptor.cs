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

        /// <summary>The <c>SOUND frame #ANIMATION name</c> lines.</summary>
        public IReadOnlyList<GpDescriptorSound> Sounds { get; private init; } = Array.Empty<GpDescriptorSound>();

        /// <summary>The <c>WEAPON attack name</c> lines: the weapon (in <c>WEAPON.NDS</c>) of each attack.</summary>
        public IReadOnlyList<GpDescriptorWeapon> Weapons { get; private init; } = Array.Empty<GpDescriptorWeapon>();

        /// <summary>
        /// The <c>SETACTIVEPOINT #ANIMATION frame …</c> lines: the frame of an attack at which the weapon fires.
        /// </summary>
        public IReadOnlyList<(string Animation, int Frame)> FiringFrames { get; private init; } = Array.Empty<(string, int)>();

        /// <summary>The <c>SETACTIVEPOINT</c> lines with their points: where the weapon is, e.g. the muzzle.</summary>
        public IReadOnlyList<GpDescriptorActivePoint> ActivePoints { get; private init; } = Array.Empty<GpDescriptorActivePoint>();

        /// <summary>The <c>LIFE</c> line: how much damage the object takes before it dies; null if it has none.</summary>
        public int? Life { get; private init; }

        /// <summary>The <c>DAMAGE attack value</c> lines: the damage each attack does.</summary>
        public IReadOnlyList<(int Attack, int Value)> Damages { get; private init; } = Array.Empty<(int, int)>();

        /// <summary>
        /// The damage of an attack animation (<c>ATTACK</c> or <c>ATTACK0</c> is attack 0, <c>ATTACK1</c>
        /// attack 1, …); null if the descriptor does not give it.
        /// </summary>
        public int? DamageOf(string animation) => ValueOf(Damages, animation);

        /// <summary>The <c>ATTACK_PAUSE attack value</c> lines: how long each attack rests before it can be made again.</summary>
        public IReadOnlyList<(int Attack, int Value)> AttackPauses { get; private init; } = Array.Empty<(int, int)>();

        /// <summary>The pause after an attack animation, as <see cref="DamageOf"/>; null if the descriptor does not give it.</summary>
        public int? AttackPauseOf(string animation) => ValueOf(AttackPauses, animation);

        /// <summary>
        /// The <c>ATTACK_RADIUS attack min max</c> lines: how far each attack reaches, in pixels on the ground.
        /// </summary>
        public IReadOnlyList<(int Attack, int Value)> AttackRanges { get; private init; } = Array.Empty<(int, int)>();

        /// <summary>How far an attack animation reaches, as <see cref="DamageOf"/>; null if the descriptor does not give it.</summary>
        public int? AttackRangeOf(string animation) => ValueOf(AttackRanges, animation);

        /// <summary>The minimum of the <c>ATTACK_RADIUS</c> lines: how near each attack can still be made.</summary>
        public IReadOnlyList<(int Attack, int Value)> AttackMinRanges { get; private init; } = Array.Empty<(int, int)>();

        /// <summary>How near an attack animation can still be made, as <see cref="AttackRangeOf"/>.</summary>
        public int? AttackMinRangeOf(string animation) => ValueOf(AttackMinRanges, animation);

        /// <summary>
        /// The <c>DET_RADIUS attack min max</c> lines, by attack: the distances, in pixels on the ground, at which
        /// the object chooses each attack.
        /// </summary>
        public IReadOnlyDictionary<int, (int Min, int Max)> DetectionRadii { get; private init; } = new Dictionary<int, (int, int)>();

        /// <summary>The distances at which an attack animation is chosen, as <see cref="DamageOf"/>; null if not given.</summary>
        public (int Min, int Max)? DetectionRadiusOf(string animation) =>
            DetectionRadii.TryGetValue(AttackNumberOf(animation), out var band) ? band : null;

        /// <summary>The <c>SEARCH_ENEMY_RADIUS</c> line: how far, in pixels on the ground, the object looks for enemies.</summary>
        public int? SearchEnemyRadius => Number(SearchEnemyRadiusKey);

        /// <summary>The <c>RAZBROS</c> line: how widely the object's shots scatter around the point they are aimed at.</summary>
        public int? Scatter => Number(ScatterKey);

        /// <summary>The <c>ROTATE</c> line: how fast the object turns; null if it turns at once.</summary>
        public int? TurnRate => Number(TurnRateKey);

        /// <summary>
        /// The <c>MOTIONSTYLE</c> line: how an object that turns slowly moves. <c>SLOWROTATE</c> (cannons, mortars)
        /// turns where it stands, <c>SHEEPS</c> (ships) as it goes; null if there is none.
        /// </summary>
        public string MotionStyle { get; private init; }

        /// <summary>Whether the object turns as it goes, like a ship, and does not stop to turn.</summary>
        public bool TurnsUnderWay => "SHEEPS".Equals(MotionStyle, StringComparison.OrdinalIgnoreCase);

        /// <summary>The <c>SELECT_SOUND</c> line: the sound (a name in <c>SOUNDLIST.TXT</c>) the object makes when selected; null if none.</summary>
        public string SelectSound => Words.GetValueOrDefault(SelectSoundKey);

        /// <summary>The <c>ORDER_SOUND</c> line: the sound the object makes when it is given an order; null if none.</summary>
        public string OrderSound => Words.GetValueOrDefault(OrderSoundKey);

        /// <summary>The <c>BORN_SOUND</c> line: the sound the object makes when it appears; null if none.</summary>
        public string BornSound => Words.GetValueOrDefault(BornSoundKey);

        /// <summary>The <c>MATHERIAL count names…</c> lines: what the object is made of (BODY, WOOD, IRON, BUILDING, …).</summary>
        public IReadOnlySet<string> Materials { get; private init; } = new HashSet<string>();

        /// <summary>
        /// The <c>ATTMASK attack count materials…</c> lines, by attack: the only materials that attack is made on.
        /// </summary>
        public IReadOnlyDictionary<int, IReadOnlySet<string>> AttackMasks { get; private init; } = new Dictionary<int, IReadOnlySet<string>>();

        /// <summary>Whether an attack animation is made on another object: always, unless its <c>ATTMASK</c> leaves out what that is made of.</summary>
        public bool CanAttack(GpDescriptor target, string animation) =>
            !AttackMasks.TryGetValue(AttackNumberOf(animation), out var mask) || mask.Overlaps(target.Materials);

        private const string SelectSoundKey = "SELECT_SOUND";
        private const string OrderSoundKey = "ORDER_SOUND";
        private const string BornSoundKey = "BORN_SOUND";

        private static readonly HashSet<string> WordKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SelectSoundKey, OrderSoundKey, BornSoundKey,
        };

        // The first word after the keyword of the lines named in WordKeys.
        private IReadOnlyDictionary<string, string> Words { get; init; } = new Dictionary<string, string>();

        /// <summary>The <c>ZPOINTS</c> line: how high above the ground the object is hit, in pixels.</summary>
        public int? Height => Number(HeightKey);

        /// <summary>The <c>GEOMETRY one radius step</c> line: the room the object takes on the ground, as a radius in pixels.</summary>
        public int? Radius { get; private init; }

        /// <summary>The step of the <c>GEOMETRY</c> line: how far the object walks at a time.</summary>
        public int? StepSize { get; private init; }

        /// <summary>The <c>SHOWDELAY</c> line: the wait before the object can attack again is shown.</summary>
        public bool ShowsDelay => Flags.Contains("SHOWDELAY");

        /// <summary>The <c>STANDGROUND</c> line: the object can be told to hold its place.</summary>
        public bool CanStandGround => Flags.Contains("STANDGROUND");

        private const string SearchEnemyRadiusKey = "SEARCH_ENEMY_RADIUS";
        private const string ScatterKey = "RAZBROS";
        private const string TurnRateKey = "ROTATE";
        private const string HeightKey = "ZPOINTS";

        private static readonly HashSet<string> NumberKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SearchEnemyRadiusKey, ScatterKey, TurnRateKey, HeightKey,
        };

        // The first number of the lines named in NumberKeys, and the lines that are one word.
        private IReadOnlyDictionary<string, int> Numbers { get; init; } = new Dictionary<string, int>();

        private IReadOnlySet<string> Flags { get; init; } = new HashSet<string>();

        private int? Number(string key) => Numbers.TryGetValue(key, out var value) ? value : null;

        /// <summary>The <c>SHIELD</c> line: armor taken off the damage of every hit; 0 if there is none.</summary>
        public int Shield { get; private init; }

        /// <summary>The <c>PROTECTION count (kind value)…</c> line: more armor against each kind of weapon.</summary>
        public IReadOnlyDictionary<string, int> Protections { get; private init; } = new Dictionary<string, int>();

        /// <summary>The <c>WEAPONKIND attack kind</c> lines: the kind of weapon of each attack (PIKA, VISTREL, …).</summary>
        public IReadOnlyList<(int Attack, string Kind)> WeaponKinds { get; private init; } = Array.Empty<(int, string)>();

        /// <summary>The kind of weapon of an attack animation, as <see cref="DamageOf"/>; null if not given.</summary>
        public string WeaponKindOf(string animation)
        {
            var attack = AttackNumberOf(animation);
            return WeaponKinds.Where(kind => kind.Attack == attack).Select(kind => kind.Kind).FirstOrDefault();
        }

        /// <summary>
        /// The damage an attack of this object does to another: its damage less the target's shield and its
        /// protection against the attack's kind of weapon, but at least 1. Null if the attack has no damage given.
        /// </summary>
        /// <param name="share">The part of the damage that reaches the target, e.g. 0.5 halfway out of a blast.</param>
        public int? DamageTo(GpDescriptor target, string animation, double share = 1)
        {
            if (DamageOf(animation) is not { } damage)
            {
                return null;
            }

            var kind = WeaponKindOf(animation);
            var protection = kind != null && target.Protections.TryGetValue(kind, out var value) ? value : 0;
            return Math.Max(1, (int) Math.Round(damage * share) - target.Shield - protection);
        }

        private static int AttackNumberOf(string animation)
        {
            var number = animation.Substring(animation.IndexOf("ATTACK", StringComparison.OrdinalIgnoreCase) + "ATTACK".Length);
            return TryInt(number, out var parsed) ? parsed : 0;
        }

        private static int? ValueOf(IReadOnlyList<(int Attack, int Value)> values, string animation)
        {
            var attack = AttackNumberOf(animation);
            return values
                .Where(value => value.Attack == attack)
                .Select(value => (int?) value.Value)
                .FirstOrDefault();
        }

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
            var sounds = new List<GpDescriptorSound>();
            var weapons = new List<GpDescriptorWeapon>();
            var firingFrames = new List<(string Animation, int Frame)>();
            var activePoints = new List<GpDescriptorActivePoint>();
            var damages = new List<(int Attack, int Value)>();
            var attackPauses = new List<(int Attack, int Value)>();
            var attackRanges = new List<(int Attack, int Value)>();
            var attackMinRanges = new List<(int Attack, int Value)>();
            var weaponKinds = new List<(int Attack, string Kind)>();
            var protections = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var detectionRadii = new Dictionary<int, (int Min, int Max)>();
            var numbers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var shield = 0;
            int? life = null;
            int? radius = null;
            int? stepSize = null;
            string motionStyle = null;
            var words = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var materials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var attackMasks = new Dictionary<int, IReadOnlySet<string>>();

            foreach (var line in text.Split('\n'))
            {
                var tokens = line.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 1)
                {
                    flags.Add(tokens[0]);
                }

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
                    // The word before the anchor is SHADOW for units and NONE for weapon effects.
                    if (tokens.Length >= 6 && TryInt(tokens[4], out var shadowX) && TryInt(tokens[5], out var shadowY))
                    {
                        anchorX = -shadowX;
                        anchorY = -shadowY;
                    }

                    sprites.Add(new GpDescriptorSprite(index, tokens[2], anchorX, anchorY));
                }
                else if (tokens.Length >= 4 && tokens[0].Equals("SOUND", StringComparison.OrdinalIgnoreCase)
                         && TryInt(tokens[1], out var soundFrame) && tokens[2].StartsWith('#'))
                {
                    sounds.Add(new GpDescriptorSound(tokens[2].Substring(1), soundFrame, tokens[3]));
                }
                else if (tokens.Length >= 3 && tokens[0].Equals("WEAPON", StringComparison.OrdinalIgnoreCase)
                         && TryInt(tokens[1], out var attack))
                {
                    weapons.Add(new GpDescriptorWeapon(attack, tokens[2]));
                }
                else if (tokens.Length >= 3 && tokens[0].Equals("SETACTIVEPOINT", StringComparison.OrdinalIgnoreCase)
                         && tokens[1].StartsWith('#') && TryInt(tokens[2], out var firingFrame))
                {
                    firingFrames.Add((tokens[1].Substring(1), firingFrame));

                    var points = new List<(int X, int Y)>();
                    for (var i = 3; i + 1 < tokens.Length && TryInt(tokens[i], out var pointX) && TryInt(tokens[i + 1], out var pointY); i += 2)
                    {
                        points.Add((pointX, pointY));
                    }

                    activePoints.Add(new GpDescriptorActivePoint(tokens[1].Substring(1), firingFrame, points));
                }
                else if (tokens[0].Equals("LIFE", StringComparison.OrdinalIgnoreCase) && TryInt(tokens[1], out var lifeValue))
                {
                    life ??= lifeValue;
                }
                else if (tokens.Length >= 3 && tokens[0].Equals("DAMAGE", StringComparison.OrdinalIgnoreCase)
                         && TryInt(tokens[1], out var damageAttack) && TryInt(tokens[2], out var damageValue))
                {
                    damages.Add((damageAttack, damageValue));
                }
                else if (tokens.Length >= 3 && tokens[0].Equals("ATTACK_PAUSE", StringComparison.OrdinalIgnoreCase)
                         && TryInt(tokens[1], out var pauseAttack) && TryInt(tokens[2], out var pauseValue))
                {
                    attackPauses.Add((pauseAttack, pauseValue));
                }
                else if (tokens.Length >= 4 && tokens[0].Equals("ATTACK_RADIUS", StringComparison.OrdinalIgnoreCase)
                         && TryInt(tokens[1], out var rangeAttack) && TryInt(tokens[2], out var rangeMin)
                         && TryInt(tokens[3], out var rangeValue))
                {
                    attackMinRanges.Add((rangeAttack, rangeMin));
                    attackRanges.Add((rangeAttack, rangeValue));
                }
                else if (tokens.Length >= 4 && tokens[0].Equals("DET_RADIUS", StringComparison.OrdinalIgnoreCase)
                         && TryInt(tokens[1], out var detectionAttack) && TryInt(tokens[2], out var detectionMin)
                         && TryInt(tokens[3], out var detectionMax))
                {
                    // Some descriptors give an attack twice: the last line counts.
                    detectionRadii[detectionAttack] = (detectionMin, detectionMax);
                }
                else if (tokens.Length >= 4 && tokens[0].Equals("GEOMETRY", StringComparison.OrdinalIgnoreCase)
                         && TryInt(tokens[2], out var geometryRadius) && TryInt(tokens[3], out var geometryStep))
                {
                    radius = geometryRadius;
                    stepSize = geometryStep;
                }
                else if (WordKeys.Contains(tokens[0]))
                {
                    words[tokens[0]] = tokens[1];
                }
                else if (tokens[0].Equals("MATHERIAL", StringComparison.OrdinalIgnoreCase) && TryInt(tokens[1], out var materialCount))
                {
                    materials.UnionWith(tokens.Skip(2).Take(materialCount));
                }
                else if (tokens.Length >= 3 && tokens[0].Equals("ATTMASK", StringComparison.OrdinalIgnoreCase)
                         && TryInt(tokens[1], out var maskAttack) && TryInt(tokens[2], out var maskCount))
                {
                    attackMasks[maskAttack] = tokens.Skip(3).Take(maskCount).ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
                else if (tokens[0].Equals("MOTIONSTYLE", StringComparison.OrdinalIgnoreCase))
                {
                    motionStyle = tokens[1];
                }
                else if (NumberKeys.Contains(tokens[0]) && TryInt(tokens[1], out var number))
                {
                    numbers[tokens[0]] = number;
                }
                else if (tokens[0].Equals("SHIELD", StringComparison.OrdinalIgnoreCase) && TryInt(tokens[1], out var shieldValue))
                {
                    shield = shieldValue;
                }
                else if (tokens.Length >= 3 && tokens[0].Equals("WEAPONKIND", StringComparison.OrdinalIgnoreCase)
                         && TryInt(tokens[1], out var kindAttack))
                {
                    weaponKinds.Add((kindAttack, tokens[2]));
                }
                else if (tokens[0].Equals("PROTECTION", StringComparison.OrdinalIgnoreCase) && TryInt(tokens[1], out var kinds))
                {
                    for (var i = 0; i < kinds && 3 + 2 * i < tokens.Length; i++)
                    {
                        if (TryInt(tokens[3 + 2 * i], out var protection))
                        {
                            protections[tokens[2 + 2 * i]] = protection;
                        }
                    }
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

            return new GpDescriptor(fileName, name ?? fileName, displayName, sprites, animations) 
            {
                Sounds = sounds,
                Weapons = weapons,
                FiringFrames = firingFrames,
                ActivePoints = activePoints,
                Life = life,
                Damages = damages,
                AttackPauses = attackPauses,
                AttackRanges = attackRanges,
                AttackMinRanges = attackMinRanges,
                DetectionRadii = detectionRadii,
                Numbers = numbers,
                Flags = flags,
                MotionStyle = motionStyle,
                Words = words,
                Materials = materials,
                AttackMasks = attackMasks,
                Radius = radius,
                StepSize = stepSize,
                Shield = shield,
                Protections = protections,
                WeaponKinds = weaponKinds,
            };
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
    /// <summary>A sound (a name in <c>SOUNDLIST.TXT</c>) played at a frame of an animation.</summary>
    public sealed record GpDescriptorSound(string Animation, int Frame, string Sound);

    /// <summary>The weapon of an attack: attack 0 is the animation <c>ATTACK</c>, attack 1 <c>ATTACK1</c>, …</summary>
    public sealed record GpDescriptorWeapon(int Attack, string Weapon)
    {
        public string Animation => Attack == 0 ? "ATTACK" : "ATTACK" + Attack;
    }

    /// <summary>
    /// Where the weapon of an attack is at the frame it fires: one point per stored direction, in the
    /// coordinates of the sprite (like its anchor).
    /// </summary>
    public sealed record GpDescriptorActivePoint(string Animation, int Frame, IReadOnlyList<(int X, int Y)> Points);

    public sealed record GpDescriptorAnimation(string Name, int Directions, IReadOnlyList<(int Sprite, int Pose)> Frames);
}
