using System;
using System.Collections.Generic;
using System.Linq;

namespace GPExtractor
{
    /// <summary>
    /// Who may stand in which formations: a line of the <c>[OFFICERS]</c> section of a nation's file. Units are
    /// named as their descriptors are (<c>NAME</c>), without the nation.
    /// </summary>
    public sealed record FormationRule(
          string Officer
        , string Drummer
        , IReadOnlySet<string> Formations
        , IReadOnlySet<string> Units
    )
    {
        /// <summary>Whether a unit of this name has a part in the rule: as its officer, its drummer or a soldier.</summary>
        public bool Allows(string unit) =>
            Units.Contains(unit)
            || Officer.Equals(unit, StringComparison.OrdinalIgnoreCase)
            || Drummer.Equals(unit, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The <c>[OFFICERS]</c> section of a nation's file (<c>RUSSIA.NDS</c>): a line <c>Officer(RU) Drummer(RU) n</c>
    /// and <c>n</c> lines <c>TYPE count #FORMATION … count Unit(RU) …</c>, the formations of one kind those two
    /// lead and the units that stand in them.
    /// </summary>
    public static class FormationRules
    {
        public static IReadOnlyList<FormationRule> Parse(string text)
        {
            var lines = text.Split('\n')
                .Select(line => line.Trim())
                .SkipWhile(line => !line.StartsWith("[OFFICERS]", StringComparison.OrdinalIgnoreCase))
                .Skip(1)
                .TakeWhile(line => !line.StartsWith('['))
                .Where(line => line.Length > 0 && !line.StartsWith('/'))
                .Select(line => line.Split((char[]) null, StringSplitOptions.RemoveEmptyEntries))
                .ToList();

            var rules = new List<FormationRule>();
            for (var i = 0; i < lines.Count;)
            {
                var header = lines[i++];
                if (header.Length < 3 || !int.TryParse(header[2], out var count))
                {
                    continue;
                }

                var officer = WithoutNation(header[0]);
                var drummer = WithoutNation(header[1]);
                for (var end = Math.Min(i + count, lines.Count); i < end; i++)
                {
                    var formations = Counted(lines[i], 1);
                    var units = Counted(lines[i], 2 + formations.Count).Select(WithoutNation).ToList();
                    rules.Add(new FormationRule(
                          officer
                        , drummer
                        , formations.Select(name => name.TrimStart('#')).ToHashSet(StringComparer.OrdinalIgnoreCase)
                        , (units.Count > 0 ? units : new List<string> { officer }).ToHashSet(StringComparer.OrdinalIgnoreCase)
                    ));
                }
            }

            return rules;
        }

        /// <summary>The tokens after a count at <paramref name="at"/>, as many as it says.</summary>
        private static IReadOnlyList<string> Counted(string[] tokens, int at) =>
            at < tokens.Length && int.TryParse(tokens[at], out var count)
                ? tokens.Skip(at + 1).Take(count).ToList()
                : Array.Empty<string>();

        /// <summary><c>Strelec(RU)</c> is the unit <c>Strelec</c>.</summary>
        private static string WithoutNation(string name)
        {
            var bracket = name.IndexOf('(');
            return bracket > 0 ? name.Substring(0, bracket) : name;
        }
    }
}
