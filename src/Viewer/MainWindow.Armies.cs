using System;
using System.Collections.Generic;
using System.Linq;
using GPExtractor;

namespace Viewer
{
    /// <summary>The sides on the field: their strength and score, and what the picked side sees.</summary>
    public partial class MainWindow
    {
        /// <summary>What a unit without a <c>VES</c> line is worth, as in the game.</summary>
        private const int DefaultWorth = 10;

        /// <summary>A side loses this many times a fallen unit's worth, and the side that killed it gains as much.</summary>
        private const int DeathWorths = 2;

        /// <summary>The game shows a score as a hundredth of what it counts.</summary>
        private const double ScoreUnit = 100;

        /// <summary>
        /// How far, in pixels on the screen, a unit sees around itself, and how much farther for each step of
        /// its <c>VISION</c>: the game lights the fog in spots this far apart.
        /// </summary>
        private const double SightNear = 200;
        private const double SightStep = 128;

        // What each side has scored, by its color.
        private readonly Dictionary<int, int> _scores = new Dictionary<int, int>();

        private static int WorthOf(FieldUnitView unit) => unit.Kind.Descriptor.Worth ?? DefaultWorth;

        private void Score(NationColorOffset side, int points) =>
            _scores[side.Offset] = _scores.GetValueOrDefault(side.Offset) + points;

        /// <summary>A unit falls: its side loses twice its worth, which the side that killed it gains.</summary>
        private void ScoreDeath(FieldUnitView unit, NationColorOffset? by)
        {
            if (unit.IsDead)
            {
                return;
            }

            Score(unit.Color, -DeathWorths * WorthOf(unit));
            if (by is { } killer && killer.Offset != unit.Color.Offset)
            {
                Score(killer, DeathWorths * WorthOf(unit));
            }
        }

        /// <summary>
        /// Each side on the field in a line: how many units it has, their strength (the sum of their
        /// <c>FORCE</c>) and its score.
        /// </summary>
        private string ArmiesSummary()
        {
            var sides = _fieldUnits
                .Where(unit => !unit.IsDead)
                .GroupBy(unit => unit.Color.Offset)
                .Select(side =>
                {
                    var name = NationColorOffset.All.FirstOrDefault(color => color.Offset.Offset == side.Key).Name ?? "Side " + side.Key;
                    var score = _scores.GetValueOrDefault(side.Key) / ScoreUnit;
                    return name + ": " + side.Count() + (side.Count() == 1 ? " unit" : " units")
                           + ", strength " + side.Sum(unit => unit.Kind.Descriptor.Force ?? 0)
                           + ", score " + score.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                });
            return string.Join("   ·   ", sides);
        }

        /// <summary>How far a unit sees: the game's sights 1 and 2 reach one step beyond the nearest, 3 to 8 two to seven.</summary>
        private static double SightOf(FieldUnitView unit)
        {
            var vision = unit.Kind.Descriptor.Vision ?? 0;
            return SightNear + SightStep * (vision <= 2 ? Math.Min(vision, 1) : vision - 1);
        }

        /// <summary>
        /// Whether a unit is shown: always, unless the fog of war is on and it is of another color than the
        /// picked one, out of sight of every living unit of that color.
        /// </summary>
        private bool IsSeen(FieldUnitView unit)
        {
            var own = PlayerColor.Offset;
            return FogBox.IsChecked != true
                   || unit.Color.Offset == own
                   || _fieldUnits.Any(viewer => viewer.Color.Offset == own && !viewer.IsDead
                                                && Distance(viewer.Movement.Position, unit.Movement.Position) <= SightOf(viewer));
        }
    }
}
