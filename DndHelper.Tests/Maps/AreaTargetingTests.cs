using DndHelper.Maps.Areas;
using DndHelper.Maps.Hexes;
using DndHelper.Maps.Maps;
using DndHelper.Maps.Tokens;
using FluentAssertions;
using Xunit;

namespace DndHelper.Tests.Maps;

public class AreaTargetingTests
{
    private static MapToken Token(string name, int q, int r) => new() { Name = name, Hex = new Hex(q, r) };

    [Fact]
    public void Finds_only_tokens_inside_the_area_nearest_first()
    {
        var map = HexMap.CreateHexagon(8);
        var area = new AreaTemplate(AreaShapeKind.Sphere, Hex.Origin, Hex.Origin, 10);
        var tokens = new[] { Token("Far", 2, 0), Token("Near", 1, 0), Token("Outside", 5, 0) };

        var targets = AreaTargeting.FindTargets(map, area, tokens);

        targets.Select(t => t.Token.Name).Should().Equal("Near", "Far");
        targets.Select(t => t.DistanceFeet).Should().Equal(5, 10);
        targets.Should().OnlyContain(t => t.Reached);
    }

    [Fact]
    public void Walls_stop_the_effect_reaching_a_token()
    {
        var map = HexMap.CreateHexagon(8);
        map.Set(new Hex(1, 0), Terrain.Wall);
        var area = new AreaTemplate(AreaShapeKind.Sphere, Hex.Origin, Hex.Origin, 20);

        var targets = AreaTargeting.FindTargets(map, area, [Token("Behind", 2, 0), Token("Beside", 0, 2)]);

        targets.Single(t => t.Token.Name == "Behind").Reached.Should().BeFalse();
        targets.Single(t => t.Token.Name == "Beside").Reached.Should().BeTrue();
    }

    [Fact]
    public void Token_on_the_origin_hex_is_reached()
    {
        var map = HexMap.CreateHexagon(3);
        var area = new AreaTemplate(AreaShapeKind.Sphere, Hex.Origin, Hex.Origin, 5);

        var targets = AreaTargeting.FindTargets(map, area, [Token("Centre", 0, 0)]);

        targets.Should().ContainSingle().Which.Reached.Should().BeTrue();
    }
}
