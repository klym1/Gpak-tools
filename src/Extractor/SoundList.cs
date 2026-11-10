using System;
using System.Collections.Generic;
using System.Linq;

namespace GPExtractor
{
    /// <param name="Files">Wave files relative to the game folder; the game plays one of them.</param>
    /// <param name="Danger">Whether the entry is marked <c>DANGER</c>.</param>
    public sealed record GameSound(string Name, IReadOnlyList<string> Files, bool Danger);

    /// <summary>
    /// The game's sounds by name (<c>SOUNDLIST.TXT</c>): entries of a line <c>NAME count</c>, an optional line
    /// <c>DANGER</c>, and <c>count</c> lines with wave files.
    /// </summary>
    public sealed class SoundList
    {
        public static readonly SoundList Empty = new SoundList(Array.Empty<GameSound>());

        private readonly Dictionary<string, GameSound> _byName;

        private SoundList(IReadOnlyList<GameSound> sounds)
        {
            Sounds = sounds;
            _byName = new Dictionary<string, GameSound>(StringComparer.OrdinalIgnoreCase);
            foreach (var sound in sounds)
            {
                _byName.TryAdd(sound.Name, sound);
            }
        }

        public IReadOnlyList<GameSound> Sounds { get; }

        public GameSound Find(string name) => _byName.GetValueOrDefault(name);

        public static SoundList Parse(string text)
        {
            var lines = text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
            var sounds = new List<GameSound>();

            for (var i = 0; i < lines.Count;)
            {
                var header = lines[i++].Split((char[]) null, StringSplitOptions.RemoveEmptyEntries);
                if (header.Length < 2 || !int.TryParse(header[1], out var count))
                {
                    continue;
                }

                var danger = i < lines.Count && lines[i].Equals("DANGER", StringComparison.OrdinalIgnoreCase);
                if (danger)
                {
                    i++;
                }

                var files = lines.Skip(i).Take(count).ToList();
                i += files.Count;
                sounds.Add(new GameSound(header[0], files, danger));
            }

            return new SoundList(sounds);
        }
    }
}
