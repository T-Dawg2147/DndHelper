using DndHelper.Maps.Hexes;
using DndHelper.Maps.Maps;
using DndHelper.Maps.Navigation;
using DndHelper.Maps.Tokens;

namespace DndHelper.Maps.Areas;

/// <summary>A token inside an area, and whether the effect actually reaches it.</summary>
public sealed record AreaTarget(MapToken Token, int DistanceFeet, bool Reached);

public static class AreaTargeting
{
    /// <summary>
    /// Finds every token standing in the area. A token is "reached" only when nothing that blocks sight lies
    /// between the area's origin and it (total cover blocks area effects, as walls do at the table).
    /// Results are ordered nearest first. Tokens on the origin hex are always reached.
    /// </summary>
    public static IReadOnlyList<AreaTarget> FindTargets(
        HexMap map,
        AreaTemplate area,
        IEnumerable<MapToken> tokens,
        int feetPerHex = 5,
        HexOrientation orientation = HexOrientation.PointyTop)
    {
        var covered = AreaShapes.Cover(area, feetPerHex, orientation);

        return tokens
            .Where(t => covered.Contains(t.Hex))
            .Select(t => new AreaTarget(
                t,
                area.Origin.DistanceFeetTo(t.Hex, feetPerHex),
                LineOfSight.HasClearLine(map, area.Origin, t.Hex)))
            .OrderBy(t => t.DistanceFeet)
            .ThenBy(t => t.Token.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
