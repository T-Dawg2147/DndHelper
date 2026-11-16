namespace DndHelper.Maps.Maps;

public enum Terrain
{
    Floor,
    Grass,
    Forest,
    Rubble,
    Mud,
    ShallowWater,
    DeepWater,
    Wall,
    Pit
}

public static class TerrainRules
{
    /// <summary>Returned by <see cref="MoveCost"/> for terrain that cannot be entered.</summary>
    public const int Impassable = int.MaxValue;

    /// <summary>Movement cost to enter a hex, in hexes of movement. Difficult terrain costs 2.</summary>
    public static int MoveCost(Terrain terrain) => terrain switch
    {
        Terrain.Wall or Terrain.Pit => Impassable,
        Terrain.Forest or Terrain.Rubble or Terrain.Mud or Terrain.ShallowWater or Terrain.DeepWater => 2,
        _ => 1
    };

    public static bool IsPassable(Terrain terrain) => MoveCost(terrain) != Impassable;

    public static bool BlocksSight(Terrain terrain) => terrain == Terrain.Wall;

    public static bool IsDifficult(Terrain terrain) => MoveCost(terrain) == 2;

    public static string DisplayName(Terrain terrain) => terrain switch
    {
        Terrain.ShallowWater => "Shallow water",
        Terrain.DeepWater => "Deep water",
        _ => terrain.ToString()
    };
}
