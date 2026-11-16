using DndHelper.Maps.Hexes;
using DndHelper.Maps.Maps;
using DndHelper.Maps.Tokens;

namespace DndHelper.Maps.Navigation;

/// <summary>Movement for tokens on a map. Other tokens block movement; terrain costs apply.</summary>
public static class MovementRules
{
    /// <summary>Every hex the token can reach with its speed, with the cost in hexes. Includes its own hex at cost 0.</summary>
    public static IReadOnlyDictionary<Hex, int> Reachable(HexMap map, MapToken token, IEnumerable<MapToken> all)
    {
        var blocked = OccupiedByOthers(token, all);
        return Pathfinder.Reachable(map, token.Hex, Budget(map, token), blocked.Contains);
    }

    /// <summary>The cheapest route to <paramref name="target"/>, or null if it is blocked or farther than the token's speed.</summary>
    public static PathResult? PathTo(HexMap map, MapToken token, Hex target, IEnumerable<MapToken> all)
    {
        var blocked = OccupiedByOthers(token, all);
        var result = Pathfinder.FindPath(map, token.Hex, target, blocked.Contains);
        return result is not null && result.Cost <= Budget(map, token) ? result : null;
    }

    /// <summary>Moves the token if the target is within reach. <paramref name="feet"/> is the distance travelled.</summary>
    public static bool TryMove(HexMap map, MapToken token, Hex target, IEnumerable<MapToken> all, out int feet)
    {
        feet = 0;
        if (target == token.Hex) return false;

        var path = PathTo(map, token, target, all);
        if (path is null) return false;

        token.Hex = target;
        feet = path.CostFeet(map);
        return true;
    }

    private static int Budget(HexMap map, MapToken token) => token.SpeedFeet / Math.Max(1, map.FeetPerHex);

    private static HashSet<Hex> OccupiedByOthers(MapToken token, IEnumerable<MapToken> all)
        => all.Where(t => t.Id != token.Id).Select(t => t.Hex).ToHashSet();
}
