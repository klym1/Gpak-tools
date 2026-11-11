using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using GPExtractor;

namespace Viewer
{
    /// <summary>Where a group of units sent to one point ends up: a block of places around it.</summary>
    public static class Formation
    {
        /// <summary>Rows are closer together than columns: the field is seen at a slant.</summary>
        public const double RowSpacing = 0.7;

        /// <summary>
        /// <paramref name="count"/> places in a block centred on <paramref name="target"/>: about as many
        /// columns as rows, <paramref name="spacing"/> pixels apart, with a shorter last row centred too.
        /// </summary>
        public static IReadOnlyList<Point> Around(Point target, int count, double spacing)
        {
            if (count <= 0)
            {
                return Array.Empty<Point>();
            }

            var columns = (int) Math.Ceiling(Math.Sqrt(count));
            var rows = (int) Math.Ceiling(count / (double) columns);
            var places = new List<Point>(count);

            for (var row = 0; row < rows; row++)
            {
                var inRow = Math.Min(columns, count - row * columns);
                for (var column = 0; column < inRow; column++)
                {
                    places.Add(new Point(
                        target.X + (column - (inRow - 1) / 2.0) * spacing,
                        target.Y + (row - (rows - 1) / 2.0) * spacing * RowSpacing));
                }
            }

            return places;
        }

        /// <summary>
        /// The places of a game formation centred on <paramref name="target"/>, a cell of its picture being
        /// <paramref name="spacing"/> pixels: <paramref name="soldiers"/> places in the order of the picture, the
        /// ones it has no room for in a block behind it, and its commanders' places. The top of the picture is
        /// the front, which looks along <paramref name="facing"/>: a direction on the ground, one long.
        /// </summary>
        public static (IReadOnlyList<Point> Soldiers, IReadOnlyList<Point> Commanders) Of(
              GameFormation formation
            , Point target
            , double spacing
            , int soldiers
            , Vector facing
        )
        {
            var left = formation.Places.Min(place => place.Column);
            var rightmost = formation.Places.Max(place => place.Column);
            var top = formation.Places.Min(place => place.Row);
            var bottom = formation.Places.Max(place => place.Row);

            var right = new Vector(-facing.Y, facing.X);

            Point At(double column, double row)
            {
                var onGround = right * ((column - (left + rightmost) / 2.0) * spacing)
                               - facing * ((row - (top + bottom) / 2.0) * spacing);
                return new Point(target.X + onGround.X, target.Y + onGround.Y * RowSpacing);
            }

            Point AtPlace((int Column, int Row) place) => At(place.Column, place.Row);

            var places = formation.Places.Take(soldiers).Select(AtPlace).ToList();
            var extra = soldiers - places.Count;
            if (extra > 0)
            {
                var rows = (int) Math.Ceiling(extra / Math.Ceiling(Math.Sqrt(extra)));
                places.AddRange(Around(At((left + rightmost) / 2.0, bottom + 2 + (rows - 1) / 2.0), extra, spacing));
            }

            return (places, formation.Commanders.Select(AtPlace).ToList());
        }

        /// <summary>
        /// Gives every unit a place, nearest pairs first, so units do not cross the whole group to get to theirs.
        /// Returns the index of the place of each unit.
        /// </summary>
        public static int[] Assign(IReadOnlyList<Point> units, IReadOnlyList<Point> places)
        {
            if (places.Count < units.Count)
            {
                throw new ArgumentException("Every unit needs a place.", nameof(places));
            }

            var pairs =
                from unit in Enumerable.Range(0, units.Count)
                from place in Enumerable.Range(0, places.Count)
                let way = places[place] - units[unit]
                orderby way.X * way.X + way.Y * way.Y, unit, place
                select (unit, place);

            var assigned = Enumerable.Repeat(-1, units.Count).ToArray();
            var taken = new bool[places.Count];
            var left = units.Count;
            foreach (var (unit, place) in pairs)
            {
                if (left == 0)
                {
                    break;
                }

                if (assigned[unit] < 0 && !taken[place])
                {
                    assigned[unit] = place;
                    taken[place] = true;
                    left--;
                }
            }

            return assigned;
        }
    }
}
