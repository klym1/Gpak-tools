using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace Viewer
{
    /// <summary>Something a unit walks around: another unit's place, with the room both need.</summary>
    public readonly record struct FieldObstacle(Point Centre, double Radius);

    /// <summary>
    /// Finds a way across the field around other units: the shortest path over a grid, straightened into as
    /// few legs as possible.
    /// </summary>
    public static class FieldPath
    {
        public const double CellSize = 4;

        /// <summary>The field is seen at a slant: a step up or down the screen is twice as far on the ground.</summary>
        public const double Slant = 2;

        private static readonly (int X, int Y)[] Neighbours =
        {
            (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1),
        };

        /// <summary>Distance on the ground between two points of the field.</summary>
        public static double GroundDistance(Point a, Point b)
        {
            var x = a.X - b.X;
            var y = (a.Y - b.Y) * Slant;
            return Math.Sqrt(x * x + y * y);
        }

        /// <summary>
        /// The points to walk to in turn, from <paramref name="from"/> to <paramref name="to"/>, keeping the
        /// obstacles' radius away from them. It ends at <paramref name="to"/>, or at the nearest free point
        /// if an obstacle is there. Units meeting head-on pass on their right.
        /// </summary>
        public static IReadOnlyList<Point> Find(
              Point from
            , Point to
            , IReadOnlyList<FieldObstacle> obstacles
            , Size field
        )
        {
            var columns = (int) Math.Ceiling(field.Width / CellSize);
            var rows = (int) Math.Ceiling(field.Height / CellSize);
            if (columns <= 0 || rows <= 0 || obstacles.Count == 0)
            {
                return new[] { to };
            }

            var blocked = new bool[columns * rows];
            foreach (var obstacle in obstacles)
            {
                // Only the cells around an obstacle can lie within it.
                var reachY = obstacle.Radius / Slant;
                var left = Math.Max(0, (int) ((obstacle.Centre.X - obstacle.Radius) / CellSize) - 1);
                var right = Math.Min(columns - 1, (int) ((obstacle.Centre.X + obstacle.Radius) / CellSize) + 1);
                var top = Math.Max(0, (int) ((obstacle.Centre.Y - reachY) / CellSize) - 1);
                var bottom = Math.Min(rows - 1, (int) ((obstacle.Centre.Y + reachY) / CellSize) + 1);
                for (var y = top; y <= bottom; y++)
                {
                    for (var x = left; x <= right; x++)
                    {
                        if (GroundDistance(CentreOf(x, y), obstacle.Centre) < obstacle.Radius)
                        {
                            blocked[y * columns + x] = true;
                        }
                    }
                }
            }

            var start = CellOf(from, columns, rows);
            var goal = CellOf(to, columns, rows);
            var goalIsFree = !blocked[goal.Y * columns + goal.X];
            if (!goalIsFree)
            {
                goal = NearestFree(goal, blocked, columns, rows);
            }

            var cells = Search(
                  start
                , goal
                , blocked
                , columns
                , rows
                , from
                , to - from
            );
            if (cells == null)
            {
                return new[] { to };
            }

            // Keep only the corners: skip every cell that the cell before it can be left for in a straight line.
            var points = cells.Select(cell => CentreOf(cell.X, cell.Y)).ToList();
            points[0] = from;
            points[^1] = goalIsFree ? to : points[^1];

            var legs = new List<Point>();
            var at = 0;
            while (at < points.Count - 1)
            {
                var next = points.Count - 1;
                while (next > at + 1 && !IsClear(points[at], points[next], blocked, columns, rows))
                {
                    next--;
                }

                legs.Add(points[next]);
                at = next;
            }

            return legs.Count > 0 ? legs : new[] { points[^1] };
        }

        private static Point CentreOf(int x, int y) => new Point((x + 0.5) * CellSize, (y + 0.5) * CellSize);

        private static (int X, int Y) CellOf(Point point, int columns, int rows) => (
            Math.Clamp((int) (point.X / CellSize), 0, columns - 1),
            Math.Clamp((int) (point.Y / CellSize), 0, rows - 1));

        private static (int X, int Y) NearestFree((int X, int Y) cell, bool[] blocked, int columns, int rows)
        {
            var seen = new HashSet<(int, int)> { cell };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue(cell);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!blocked[current.Y * columns + current.X])
                {
                    return current;
                }

                foreach (var (dx, dy) in Neighbours)
                {
                    var next = (X: current.X + dx, Y: current.Y + dy);
                    if (next.X >= 0 && next.Y >= 0 && next.X < columns && next.Y < rows && seen.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return cell;
        }

        /// <summary>A* over the free cells; null if the goal cannot be reached.</summary>
        private static List<(int X, int Y)> Search(
              (int X, int Y) start
            , (int X, int Y) goal
            , bool[] blocked
            , int columns
            , int rows
            , Point from
            , Vector way
        )
        {
            var cost = new double[columns * rows];
            Array.Fill(cost, double.MaxValue);
            var cameFrom = new int[columns * rows];
            Array.Fill(cameFrom, -1);

            var open = new PriorityQueue<(int X, int Y), double>();
            cost[start.Y * columns + start.X] = 0;
            open.Enqueue(start, 0);


            while (open.Count > 0)
            {
                var current = open.Dequeue();
                if (current == goal)
                {
                    var path = new List<(int X, int Y)>();
                    for (var index = goal.Y * columns + goal.X; index >= 0; index = cameFrom[index])
                    {
                        path.Add((index % columns, index / columns));
                    }

                    path.Reverse();
                    return path;
                }

                var currentIndex = current.Y * columns + current.X;
                foreach (var (dx, dy) in Neighbours)
                {
                    var next = (X: current.X + dx, Y: current.Y + dy);
                    if (next.X < 0 || next.Y < 0 || next.X >= columns || next.Y >= rows)
                    {
                        continue;
                    }

                    var nextIndex = next.Y * columns + next.X;
                    var isStart = next == start;
                    if (blocked[nextIndex] && !isStart)
                    {
                        continue;
                    }

                    // No squeezing diagonally between two blocked cells.
                    if (dx != 0 && dy != 0 && (blocked[current.Y * columns + next.X] || blocked[next.Y * columns + current.X]))
                    {
                        continue;
                    }

                    // Left of the straight way costs a little more, so two units meeting each go round on their right.
                    var aside = CentreOf(next.X, next.Y) - from;
                    var onLeft = way.X * aside.Y - way.Y * aside.X < 0;
                    var step = Math.Sqrt(dx * dx + dy * dy * Slant * Slant) * (onLeft ? 1.05 : 1);
                    var newCost = cost[currentIndex] + step;
                    if (newCost >= cost[nextIndex])
                    {
                        continue;
                    }

                    cost[nextIndex] = newCost;
                    cameFrom[nextIndex] = currentIndex;
                    double remainingX = goal.X - next.X, remainingY = (goal.Y - next.Y) * Slant;
                    open.Enqueue(next, newCost + Math.Sqrt(remainingX * remainingX + remainingY * remainingY));
                }
            }

            return null;
        }

        /// <summary>Whether the straight line between two points crosses no blocked cell.</summary>
        private static bool IsClear(Point from, Point to, bool[] blocked, int columns, int rows)
        {
            var way = to - from;
            var steps = (int) Math.Ceiling(Math.Max(Math.Abs(way.X), Math.Abs(way.Y)) / (CellSize / 4));
            for (var i = 1; i < steps; i++)
            {
                var cell = CellOf(from + way * (i / (double) steps), columns, rows);
                if (blocked[cell.Y * columns + cell.X])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
