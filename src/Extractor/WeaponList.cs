using System;
using System.Collections.Generic;
using System.Linq;

namespace GPExtractor
{
    /// <summary>
    /// The game's weapons (<c>WEAPON.NDS</c>), as far as their sounds go. The file has sections like
    /// <c>[SOUND]</c> (lines <c>weapon sound</c>), <c>[SYNCWEAPON]</c> (lines <c>weapon count weapons…</c>:
    /// weapons launched together with one) and <c>[CHILDWEAPON]</c> (lines <c>weapon min max frame count
    /// weapons…</c>: weapons born when one explodes), <c>[MEMBERS]</c> (lines <c>weapon #animation damage
    /// radius transparency gravity propagation speed cycles</c>), and <c>//</c> comments.
    /// </summary>
    /// <summary>
    /// A weapon that is drawn where it appears: its animation and how it is laid over what is behind it
    /// (<c>NONE</c>: as it is; <c>WHITE</c>, <c>DARK</c>, …: see <see cref="GameCatalog.BlendTable"/>).
    /// </summary>
    public sealed record WeaponEffect(string Animation, string Transparency);

    /// <summary>
    /// A weapon in flight: its animation, the angle above the ground it is launched at, in degrees, and how
    /// far from where it lands it does damage.
    /// </summary>
    public sealed record WeaponFlight(string Animation, double LaunchAngle, double BlastRadius);

    public sealed class WeaponList
    {
        public static readonly WeaponList Empty = new WeaponList(
              new Dictionary<string, string>()
            , new Dictionary<string, IReadOnlyList<string>>()
            , new Dictionary<string, IReadOnlyList<string>>()
        );

        private const int MaxDepth = 8;

        // Weapons that stay where they appear (smoke, explosions); the others fly.
        private static readonly string[] StationaryPropagations = { "STAND", "SLIGHTUP", "IMMEDIATE" };

        // The animation of weapons that are not drawn.
        private const string NoAnimation = "POINT";

        private Dictionary<string, (string Animation, bool Stays, bool Travels, double LaunchAngle, double Radius, string Transparency)> Members { get; init; } =
            new Dictionary<string, (string, bool, bool, double, double, string)>(StringComparer.OrdinalIgnoreCase);

        // The angle a weapon is taken to be launched at when the file gives its speed instead: an arrow's.
        private const double DefaultLaunchAngle = 15;

        private readonly Dictionary<string, string> _sounds;
        private readonly Dictionary<string, IReadOnlyList<string>> _launchedTogether;
        private readonly Dictionary<string, IReadOnlyList<string>> _bornOnExplosion;

        private WeaponList(
              Dictionary<string, string> sounds
            , Dictionary<string, IReadOnlyList<string>> launchedTogether
            , Dictionary<string, IReadOnlyList<string>> bornOnExplosion
        )
        {
            _sounds = sounds;
            _launchedTogether = launchedTogether;
            _bornOnExplosion = bornOnExplosion;
        }

        /// <summary>
        /// The name (in <c>SOUNDLIST.TXT</c>) of the sound of firing a weapon: its own, or that of the nearest
        /// weapon launched together with it that has one. Null if it is silent.
        /// </summary>
        public string ShotSoundOf(string weapon)
        {
            return NearestSound(
                  weapon
                , candidate => _launchedTogether.GetValueOrDefault(candidate) ?? Array.Empty<string>()
                , null
            );
        }

        /// <summary>
        /// The name of the sound of a weapon's impact: that of the nearest weapon born when it, or a weapon
        /// launched with it, explodes. Null if it lands silently.
        /// </summary>
        public string ImpactSoundOf(string weapon)
        {
            return NearestSound(
                  weapon
                , candidate => (_launchedTogether.GetValueOrDefault(candidate) ?? Array.Empty<string>())
                    .Concat(_bornOnExplosion.GetValueOrDefault(candidate) ?? Array.Empty<string>())
                , ShotSoundOf(weapon)
            );
        }

        /// <summary>
        /// The animations (in <c>WEAPON.ADS</c>) shown when a weapon is fired: at the muzzle, those of the
        /// weapon and of weapons launched with it that stay in place (smoke); at the impact, those of the
        /// nearest such weapons that a travelling one leads to (explosions, of which the game shows one).
        /// A weapon travels if it flies for as long as it takes; one that flies for a set number of cycles
        /// ends about where it started.
        /// </summary>
        /// <returns>
        /// Also the weapons that travel from the muzzle to the impact and are drawn (a cannonball, an arrow),
        /// with the angle above the ground they are launched at: 0 for one that flies straight.
        /// </returns>
        public (IReadOnlyList<WeaponEffect> Muzzle, IReadOnlyList<WeaponEffect> Impact, IReadOnlyList<WeaponFlight> Flying) EffectsOf(string weapon)
        {
            var muzzle = new List<WeaponEffect>();
            var impact = new List<WeaponEffect>();
            var flying = new List<WeaponFlight>();
            var level = new List<(string Weapon, bool Landed)> { (weapon, false) };

            for (var depth = 0; depth < MaxDepth && level.Count > 0 && impact.Count == 0; depth++)
            {
                var next = new List<(string Weapon, bool Landed)>();
                foreach (var (name, landed) in level)
                {
                    var travels = false;
                    if (Members.TryGetValue(name, out var member))
                    {
                        travels = member.Travels;
                        var drawn = !member.Animation.Equals(NoAnimation, StringComparison.OrdinalIgnoreCase);
                        if (member.Stays && drawn)
                        {
                            (landed ? impact : muzzle).Add(new WeaponEffect(member.Animation.ToUpperInvariant(), member.Transparency));
                        }
                        else if (member.Travels && drawn && !landed)
                        {
                            flying.Add(new WeaponFlight(member.Animation, member.LaunchAngle, member.Radius));
                        }
                    }

                    next.AddRange((_launchedTogether.GetValueOrDefault(name) ?? Array.Empty<string>())
                        .Concat(_bornOnExplosion.GetValueOrDefault(name) ?? Array.Empty<string>())
                        .Select(linked => (linked, landed || travels)));
                }

                level = next;
            }

            return (
                  muzzle.Distinct().ToList()
                , impact.Distinct().ToList()
                , flying.Distinct().ToList()
            );
        }

        /// <summary>The sound, other than <paramref name="except"/>, of the weapon or the nearest one linked to it.</summary>
        private string NearestSound(string weapon, Func<string, IEnumerable<string>> linked, string except)
        {
            var level = new List<string> { weapon };
            for (var depth = 0; depth < MaxDepth && level.Count > 0; depth++)
            {
                foreach (var candidate in level)
                {
                    if (_sounds.TryGetValue(candidate, out var sound) && sound != except)
                    {
                        return sound;
                    }
                }

                level = level.SelectMany(linked).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            return null;
        }

        public static WeaponList Parse(string text)
        {
            var sounds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var launchedTogether = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            var bornOnExplosion = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            var members = new Dictionary<string, (string Animation, bool Stays, bool Travels, double LaunchAngle, double Radius, string Transparency)>(StringComparer.OrdinalIgnoreCase);
            var section = "";

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith('['))
                {
                    section = line.Trim('[', ']').ToUpperInvariant();
                    continue;
                }

                var tokens = line.Split((char[]) null, StringSplitOptions.RemoveEmptyEntries);
                if (section == "MEMBERS" && tokens.Length >= 7 && tokens[1].StartsWith('#'))
                {
                    var stays = StationaryPropagations.Contains(tokens[6], StringComparer.OrdinalIgnoreCase);
                    var endless = tokens.Length >= 9 && int.TryParse(tokens[8], out var cycles) && cycles < 0;

                    // "ANGLE n" gives the launch angle; other weapons under gravity are taken to rise a little.
                    var angle = 0.0;
                    if (tokens[6].Equals("ANGLE", StringComparison.OrdinalIgnoreCase) && tokens.Length >= 8)
                    {
                        double.TryParse(tokens[7], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out angle);
                    }
                    else if (!tokens[5].Equals("NO_GRAVITY", StringComparison.OrdinalIgnoreCase))
                    {
                        angle = DefaultLaunchAngle;
                    }

                    double.TryParse(tokens[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var radius);
                    members.TryAdd(tokens[0], (tokens[1].Substring(1), stays, !stays && endless, angle, radius, tokens[4].ToUpperInvariant()));
                }
                else if (section == "SOUND" && tokens.Length >= 2)
                {
                    sounds.TryAdd(tokens[0], tokens[1]);
                }
                else if (section == "SYNCWEAPON" && tokens.Length >= 3)
                {
                    launchedTogether.TryAdd(tokens[0], tokens.Skip(2).ToList());
                }
                else if (section == "CHILDWEAPON" && tokens.Length >= 6)
                {
                    bornOnExplosion.TryAdd(tokens[0], tokens.Skip(5).ToList());
                }
            }

            return new WeaponList(
                  sounds
                , launchedTogether
                , bornOnExplosion
            )
            {
                Members = members,
            };
        }
    }
}
