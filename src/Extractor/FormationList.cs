using System;
using System.Collections.Generic;
using System.Linq;

namespace GPExtractor
{
    /// <summary>A formation of <c>ORDERS.LST</c>: where its soldiers and its commanders stand, in cells of its picture.</summary>
    /// <param name="Name">e.g. <c>SQUARE36</c>.</param>
    public sealed record GameFormation(
          string Name
        , IReadOnlyList<(int Column, int Row)> Places
        , IReadOnlyList<(int Column, int Row)> Commanders
    )
    {
        public override string ToString() => Name + "  ·  " + Places.Count;
    }

    /// <summary>
    /// The game's formations (<c>ORDERS.LST</c>): blocks of a line <c>#NAME SYMn …</c>, the rows of a picture in
    /// which <c>*</c> is a soldier's place and <c>C</c> a commander's, and a line <c>#END</c>. <c>#EXIT</c> ends the list.
    /// </summary>
    public static class FormationList
    {
        public static IReadOnlyList<GameFormation> Parse(string text)
        {
            var formations = new List<GameFormation>();
            string name = null;
            var places = new List<(int Column, int Row)>();
            var commanders = new List<(int Column, int Row)>();
            var row = 0;

            foreach (var line in text.Split('\n').Select(line => line.TrimEnd('\r')))
            {
                if (line.StartsWith('#'))
                {
                    var code = line.Substring(1).Split((char[]) null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                    if (code.Equals("EXIT", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (name != null && places.Count > 0)
                    {
                        formations.Add(new GameFormation(name, places.ToList(), commanders.ToList()));
                    }

                    name = code.Equals("END", StringComparison.OrdinalIgnoreCase) || code.Length == 0 ? null : code;
                    places.Clear();
                    commanders.Clear();
                    row = 0;
                }
                else if (name != null)
                {
                    for (var column = 0; column < line.Length; column++)
                    {
                        if (line[column] == '*')
                        {
                            places.Add((column, row));
                        }
                        else if (line[column] == 'C' || line[column] == 'c')
                        {
                            commanders.Add((column, row));
                        }
                    }

                    row++;
                }
            }

            return formations;
        }
    }
}
