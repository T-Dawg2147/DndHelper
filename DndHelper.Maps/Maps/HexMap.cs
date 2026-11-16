using DndHelper.Maps.Hexes;

namespace DndHelper.Maps.Maps;

/// <summary>A hex map: a set of hexes, each with a terrain. Hexes outside the set do not exist.</summary>
public sealed class HexMap
{
    private readonly Dictionary<Hex, Terrain> _cells = [];

    public string Name { get; set; } = "Untitled map";

    /// <summary>How many feet one hex is across.</summary>
    public int FeetPerHex { get; set; } = 5;

    public int Count => _cells.Count;

    public IEnumerable<Hex> Hexes => _cells.Keys;

    public IEnumerable<KeyValuePair<Hex, Terrain>> Cells => _cells;

    public bool Contains(Hex hex) => _cells.ContainsKey(hex);

    public Terrain? TerrainAt(Hex hex) => _cells.TryGetValue(hex, out var terrain) ? terrain : null;

    /// <summary>Adds the hex or changes its terrain.</summary>
    public void Set(Hex hex, Terrain terrain) => _cells[hex] = terrain;

    public bool Remove(Hex hex) => _cells.Remove(hex);

    public void Fill(IEnumerable<Hex> hexes, Terrain terrain)
    {
        foreach (var hex in hexes)
            Set(hex, terrain);
    }

    public bool IsPassable(Hex hex) => _cells.TryGetValue(hex, out var t) && TerrainRules.IsPassable(t);

    /// <summary>Hexes of movement needed to enter this hex, or <see cref="TerrainRules.Impassable"/> if it is off the map or blocked.</summary>
    public int MoveCost(Hex hex) => _cells.TryGetValue(hex, out var t) ? TerrainRules.MoveCost(t) : TerrainRules.Impassable;

    public bool BlocksSight(Hex hex) => !_cells.TryGetValue(hex, out var t) || TerrainRules.BlocksSight(t);

    /// <summary>The hex area within <paramref name="radius"/> of the origin, filled with <paramref name="terrain"/>.</summary>
    public static HexMap CreateHexagon(int radius, Terrain terrain = Terrain.Floor)
    {
        if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));

        var map = new HexMap();
        map.Fill(Hex.Origin.Spiral(radius), terrain);
        return map;
    }

    /// <summary>A roughly rectangular pointy-top map with straight left and right edges (offset rows).</summary>
    public static HexMap CreateRectangle(int columns, int rows, Terrain terrain = Terrain.Floor)
    {
        if (columns < 1) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows < 1) throw new ArgumentOutOfRangeException(nameof(rows));

        var map = new HexMap();
        for (var row = 0; row < rows; row++)
        {
            var offset = row >> 1;
            for (var column = 0; column < columns; column++)
                map.Set(new Hex(column - offset, row), terrain);
        }

        return map;
    }

    /// <summary>Smallest and largest q and r present, or null when the map is empty.</summary>
    public (int MinQ, int MaxQ, int MinR, int MaxR)? Bounds()
    {
        if (_cells.Count == 0) return null;

        var minQ = int.MaxValue; var maxQ = int.MinValue;
        var minR = int.MaxValue; var maxR = int.MinValue;

        foreach (var hex in _cells.Keys)
        {
            minQ = Math.Min(minQ, hex.Q); maxQ = Math.Max(maxQ, hex.Q);
            minR = Math.Min(minR, hex.R); maxR = Math.Max(maxR, hex.R);
        }

        return (minQ, maxQ, minR, maxR);
    }
}
