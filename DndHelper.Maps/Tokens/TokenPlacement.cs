using DndHelper.Maps.Hexes;
using DndHelper.Maps.Maps;

namespace DndHelper.Maps.Tokens;

public static class TokenPlacement
{
    /// <summary>The passable, unoccupied hex closest to <paramref name="near"/>, or null when the map has none.</summary>
    public static Hex? FindFreeHex(HexMap map, IReadOnlySet<Hex> occupied, Hex near)
        => map.Hexes
            .Where(h => map.IsPassable(h) && !occupied.Contains(h))
            .OrderBy(h => h.DistanceTo(near))
            .ThenBy(h => h.R)
            .ThenBy(h => h.Q)
            .Select(h => (Hex?)h)
            .FirstOrDefault();
}

public static class TokenNaming
{
    /// <summary>Returns the name unchanged if unused, otherwise "Name 2", "Name 3" and so on.</summary>
    public static string Unique(string name, IEnumerable<string> existing)
    {
        var taken = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseName = string.IsNullOrWhiteSpace(name) ? "Token" : name.Trim();

        if (!taken.Contains(baseName)) return baseName;

        for (var n = 2; ; n++)
        {
            var candidate = $"{baseName} {n}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }
}
