using System;
using System.Collections.Generic;
using System.Linq;

namespace GPExtractor
{
    /// <summary>
    /// The game's weapons (<c>WEAPON.NDS</c>), as far as their sounds go. The file has sections like
    /// <c>[SOUND]</c> (lines <c>weapon sound</c>), <c>[SYNCWEAPON]</c> (lines <c>weapon count weapons…</c>:
    /// weapons launched together with one) and <c>[CHILDWEAPON]</c> (lines <c>weapon min max frame count
    /// weapons…</c>: weapons born when one explodes), and <c>//</c> comments.
    /// </summary>
    public sealed class WeaponList
    {
        public static readonly WeaponList Empty = new WeaponList(
              new Dictionary<string, string>()
            , new Dictionary<string, IReadOnlyList<string>>()
            , new Dictionary<string, IReadOnlyList<string>>()
        );

        private const int MaxDepth = 8;

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
                if (section == "SOUND" && tokens.Length >= 2)
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
            );
        }
    }
}
