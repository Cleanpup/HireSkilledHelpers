using Microsoft.Xna.Framework;

namespace HireSkilledHelpers.Workers;

/// <summary>Bounded, map-sized walking search; never crosses water/walls to reach an isolated bank.</summary>
internal static class WorkerFishingShoreSearch
{
    public const string NoSpotState = "No fishing spot";
    public const string NoFarmSpotMessage = "No accessible fishing spot on this farm.";

    public static IEnumerable<Point> FromFarmEntrance(int width, int height, Point entrance, Func<Point, bool> traversable)
    {
        bool Open(Point tile) => tile.X >= 0 && tile.Y >= 0 && tile.X < width && tile.Y < height && traversable(tile);
        // The entry can lie on a small porch inside the farmhouse footprint.
        // Fishing's authored entrance teleport lands in the main walkable Farm area.
        return WorkerIdleRegionPolicy.TryFindLargest(entrance, Open, width * height, out _, out List<Point> region)
            ? region : Array.Empty<Point>();
    }
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
