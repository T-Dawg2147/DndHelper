using DndHelper.Maps.Hexes;
using DndHelper.Maps.Maps;

namespace DndHelper.Maps.Navigation;

public sealed record PathResult(IReadOnlyList<Hex> Path, int Cost)
{
    /// <summary>Cost in feet, using the map's hex size.</summary>
    public int CostFeet(HexMap map) => Cost * map.FeetPerHex;
}

public static class Pathfinder
{
    /// <summary>
    /// Cheapest route from <paramref name="start"/> to <paramref name="goal"/> (A*), or null when there is none.
    /// The start hex is included in the path but costs nothing. <paramref name="isBlocked"/> marks hexes that cannot be
    /// entered, such as hexes occupied by enemies.
    /// </summary>
    public static PathResult? FindPath(HexMap map, Hex start, Hex goal, Func<Hex, bool>? isBlocked = null)
    {
        if (!map.Contains(start) || !CanEnter(map, goal, isBlocked)) return null;
        if (start == goal) return new PathResult([start], 0);

        var best = new Dictionary<Hex, int> { [start] = 0 };
        var cameFrom = new Dictionary<Hex, Hex>();
        var queue = new PriorityQueue<Hex, int>();
        queue.Enqueue(start, start.DistanceTo(goal));

        while (queue.TryDequeue(out var current, out _))
        {
            if (current == goal)
                return new PathResult(Rebuild(cameFrom, current), best[current]);

            foreach (var next in current.Neighbors())
            {
                if (!CanEnter(map, next, isBlocked)) continue;

                var cost = best[current] + map.MoveCost(next);
                if (best.TryGetValue(next, out var known) && known <= cost) continue;

                best[next] = cost;
                cameFrom[next] = current;
                queue.Enqueue(next, cost + next.DistanceTo(goal));
            }
        }

        return null;
    }

    /// <summary>Every hex reachable within <paramref name="budget"/> hexes of movement, with the cheapest cost to reach each.</summary>
    public static IReadOnlyDictionary<Hex, int> Reachable(HexMap map, Hex start, int budget, Func<Hex, bool>? isBlocked = null)
    {
        var best = new Dictionary<Hex, int>();
        if (!map.Contains(start) || budget < 0) return best;

        best[start] = 0;
        var queue = new PriorityQueue<Hex, int>();
        queue.Enqueue(start, 0);

        while (queue.TryDequeue(out var current, out var spent))
        {
            if (spent > best[current]) continue;

            foreach (var next in current.Neighbors())
            {
                if (!CanEnter(map, next, isBlocked)) continue;

                var cost = spent + map.MoveCost(next);
                if (cost > budget) continue;
                if (best.TryGetValue(next, out var known) && known <= cost) continue;

                best[next] = cost;
                queue.Enqueue(next, cost);
            }
        }

        return best;
    }

    private static bool CanEnter(HexMap map, Hex hex, Func<Hex, bool>? isBlocked)
        => map.IsPassable(hex) && (isBlocked is null || !isBlocked(hex));

    private static List<Hex> Rebuild(Dictionary<Hex, Hex> cameFrom, Hex end)
    {
        var path = new List<Hex> { end };
        while (cameFrom.TryGetValue(end, out var previous))
        {
            path.Add(previous);
            end = previous;
        }

        path.Reverse();
        return path;
    }
}
