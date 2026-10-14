using Microsoft.Xna.Framework;

namespace HireSkilledHelpers.Workers;

/// <summary>Bounded, map-sized walking search; never crosses water/walls to reach an isolated bank.</summary>
internal static class WorkerFishingShoreSearch
{
    public const string NoSpotState = "No fishing spot";
    public const string NoFarmSpotMessage = "No accessible fishing spot on this farm.";
    public static IEnumerable<Point> ReachableTiles(int width, int height, Point start, Func<Point, bool> traversable)
    {
        bool OnMap(Point p) => p.X >= 0 && p.Y >= 0 && p.X < width && p.Y < height;
        if (!OnMap(start) || !traversable(start)) yield break;
        Queue<Point> queue = new();
        HashSet<Point> visited = new() { start };
        queue.Enqueue(start);
        Point[] directions = { new(0, 1), new(1, 0), new(0, -1), new(-1, 0) };
        while (queue.TryDequeue(out Point tile))
        {
            yield return tile;
            foreach (Point direction in directions)
            {
                Point next = tile + direction;
                if (OnMap(next) && visited.Add(next) && traversable(next)) queue.Enqueue(next);
            }
        }
    }
}
