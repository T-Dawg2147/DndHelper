using DndHelper.Maps.Generation;
using DndHelper.Maps.Maps;
using FluentAssertions;
using Xunit;

namespace DndHelper.Tests.Maps;

public class MapGeneratorTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var style in Enum.GetValues<MapStyle>())
        {
            for (var seed = 1; seed <= 25; seed++)
                yield return [style, seed];
        }
    }

    private static HexMap Make(MapStyle style, int seed, int columns = 30, int rows = 22)
        => MapGenerator.Generate(new GenerationOptions(style, columns, rows, seed));

    [Theory]
    [MemberData(nameof(Cases))]
    public void All_walkable_ground_is_connected(MapStyle style, int seed)
        => MapGenerator.Regions(Make(style, seed)).Count.Should().Be(1);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Maps_have_a_sensible_amount_of_open_ground(MapStyle style, int seed)
    {
        var map = Make(style, seed);
        var open = map.Hexes.Count(map.IsPassable);

        open.Should().BeGreaterThan(map.Count / 10);
    }

    [Theory]
    [InlineData(MapStyle.Meadow)]
    [InlineData(MapStyle.Cave)]
    [InlineData(MapStyle.Dungeon)]
    [InlineData(MapStyle.Crypt)]
    public void Same_seed_gives_the_same_map(MapStyle style)
    {
        var a = Make(style, 42).Cells.OrderBy(c => c.Key.R).ThenBy(c => c.Key.Q).ToList();
        var b = Make(style, 42).Cells.OrderBy(c => c.Key.R).ThenBy(c => c.Key.Q).ToList();

        a.Should().Equal(b);
    }

    [Theory]
    [InlineData(MapStyle.Cave)]
    [InlineData(MapStyle.Dungeon)]
    public void Different_seeds_give_different_maps(MapStyle style)
    {
        var a = Make(style, 1).Cells.Where(c => c.Value == Terrain.Wall).Select(c => c.Key).ToHashSet();
        var b = Make(style, 2).Cells.Where(c => c.Value == Terrain.Wall).Select(c => c.Key).ToHashSet();

        a.SetEquals(b).Should().BeFalse();
    }

    [Fact]
    public void Dungeon_has_walls_and_floors()
    {
        var map = Make(MapStyle.Dungeon, 7);

        map.Cells.Should().Contain(c => c.Value == Terrain.Wall);
        map.Cells.Should().Contain(c => c.Value == Terrain.Floor);
    }

    [Fact]
    public void Requested_size_is_clamped_and_named_after_style_and_seed()
    {
        var map = MapGenerator.Generate(new GenerationOptions(MapStyle.Meadow, 1, 1000, 5));

        map.Count.Should().BeLessThanOrEqualTo(MapGenerator.MaxSize * MapGenerator.MaxSize);
        map.Count.Should().BeGreaterThanOrEqualTo(MapGenerator.MinSize * MapGenerator.MinSize);
        map.Name.Should().Be("Meadow (seed 5)");
    }

    [Theory]
    [InlineData(MapStyle.Cave)]
    [InlineData(MapStyle.Dungeon)]
    [InlineData(MapStyle.Crypt)]
    public void Minimum_size_maps_still_work(MapStyle style)
        => MapGenerator.Regions(Make(style, 3, 8, 8)).Count.Should().Be(1);
}
