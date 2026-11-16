using DndHelper.Maps.Hexes;
using DndHelper.Maps.Maps;

namespace DndHelper.Maps.Navigation;

public static class LineOfSight
{
    /// <summary>
    /// True when no hex strictly between the two ends blocks sight. Hexes off the map block sight.
    /// The end hexes themselves never block, so you can see a creature standing in a wall doorway.
    /// </summary>
    public static bool HasClearLine(HexMap map, Hex from, Hex to)
    {
        var line = Hex.Line(from, to);

        for (var i = 1; i < line.Count - 1; i++)
        {
            if (map.BlocksSight(line[i])) return false;
        }

        return true;
    }

    /// <summary>All map hexes within <paramref name="radius"/> that <paramref name="from"/> can see.</summary>
    public static IReadOnlyList<Hex> Visible(HexMap map, Hex from, int radius)
        => from.Spiral(radius).Where(h => map.Contains(h) && HasClearLine(map, from, h)).ToList();
}
