using DndHelper.Maps.Areas;
using DndHelper.Maps.Hexes;
using FluentAssertions;
using Xunit;

namespace DndHelper.Tests.Maps;

public class AreaShapeTests
{
    private static IReadOnlySet<Hex> Cover(AreaShapeKind kind, Hex aim, int size, int width = 5,
        HexOrientation orientation = HexOrientation.PointyTop)
        => AreaShapes.Cover(new AreaTemplate(kind, Hex.Origin, aim, size, width), 5, orientation);

    [Theory]
    [InlineData(5, 7)]
    [InlineData(10, 19)]
    [InlineData(20, 61)]
    public void Sphere_covers_all_hexes_within_radius(int feet, int count)
        => Cover(AreaShapeKind.Sphere, new Hex(1, 0), feet).Count.Should().Be(count);

    [Fact]
    public void Sphere_includes_its_origin_and_cylinder_matches()
    {
        Cover(AreaShapeKind.Sphere, new Hex(1, 0), 10).Should().Contain(Hex.Origin);
        Cover(AreaShapeKind.Cylinder, new Hex(1, 0), 10)
            .Should().BeEquivalentTo(Cover(AreaShapeKind.Sphere, new Hex(1, 0), 10));
    }

    [Fact]
    public void Line_east_covers_a_straight_row_and_skips_origin()
    {
        var hexes = Cover(AreaShapeKind.Line, new Hex(3, 0), 30);

        hexes.Should().BeEquivalentTo(Enumerable.Range(1, 6).Select(q => new Hex(q, 0)));
    }

    [Fact]
    public void Wider_line_covers_more_hexes()
    {
        var narrow = Cover(AreaShapeKind.Line, new Hex(3, 0), 30, 5);
        var wide = Cover(AreaShapeKind.Line, new Hex(3, 0), 30, 10);

        wide.Count.Should().BeGreaterThan(narrow.Count);
        wide.Should().IntersectWith(narrow);
    }

    [Fact]
    public void Cone_is_symmetric_and_widens_with_distance()
    {
        var cone = Cover(AreaShapeKind.Cone, new Hex(1, 0), 15);

        cone.Should().Contain(new Hex(1, 0));
        cone.Should().Contain(new Hex(3, 0));
        cone.Should().NotContain(Hex.Origin);
        cone.Should().NotContain(new Hex(-1, 0));
        cone.Should().OnlyContain(h => h.DistanceTo(Hex.Origin) <= 3);

        // Mirror across the aim axis: row r and its mirror hold the same count.
        var byRow = cone.GroupBy(h => h.R).ToDictionary(g => g.Key, g => g.Count());
        byRow.Keys.Should().OnlyContain(r => r >= -2 && r <= 2);
    }

    [Fact]
    public void Cone_rotated_by_sixty_degrees_has_the_same_size()
    {
        var east = Cover(AreaShapeKind.Cone, new Hex(1, 0), 30);
        var northEast = Cover(AreaShapeKind.Cone, new Hex(1, -1), 30);

        northEast.Count.Should().Be(east.Count);
        northEast.Should().BeEquivalentTo(east.Select(h => h.RotateAround(Hex.Origin, -1)));
    }

    [Fact]
    public void Cube_extends_forward_and_to_each_side()
    {
        var cube = Cover(AreaShapeKind.Cube, new Hex(1, 0), 15);

        cube.Should().Contain(new Hex(3, 0));
        cube.Should().Contain(new Hex(1, 0));
        cube.Should().NotContain(Hex.Origin);
        cube.Should().NotContain(new Hex(4, 0));
        cube.Count.Should().BeInRange(8, 12);
    }

    [Fact]
    public void Aiming_at_the_origin_defaults_to_east()
        => Cover(AreaShapeKind.Line, Hex.Origin, 15)
            .Should().BeEquivalentTo([new Hex(1, 0), new Hex(2, 0), new Hex(3, 0)]);

    [Fact]
    public void Flat_top_shapes_work_too()
    {
        var cone = Cover(AreaShapeKind.Cone, new Hex(0, 1), 15, orientation: HexOrientation.FlatTop);

        cone.Should().Contain(new Hex(0, 1));
        cone.Should().NotBeEmpty();
    }

    [Fact]
    public void Non_positive_size_covers_nothing()
        => Cover(AreaShapeKind.Cone, new Hex(1, 0), 0).Should().BeEmpty();

    [Theory]
    [InlineData("Sphere", AreaShapeKind.Sphere)]
    [InlineData("radius", AreaShapeKind.Sphere)]
    [InlineData("CONE", AreaShapeKind.Cone)]
    [InlineData("line", AreaShapeKind.Line)]
    [InlineData("cube", AreaShapeKind.Cube)]
    [InlineData("cylinder", AreaShapeKind.Cylinder)]
    public void ParseKind_recognises_spell_shapes(string text, AreaShapeKind expected)
        => AreaTemplate.ParseKind(text).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("blob")]
    public void ParseKind_returns_null_for_unknown(string? text)
        => AreaTemplate.ParseKind(text).Should().BeNull();
}
