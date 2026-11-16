using DndHelper.Maps.Hexes;
using DndHelper.Maps.Maps;
using DndHelper.Maps.Navigation;
using FluentAssertions;

namespace DndHelper.Tests.Maps;

public class HexMapTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 7)]
    [InlineData(3, 37)]
    [InlineData(10, 331)]
    public void Hexagon_HasCentredHexCount(int radius, int count)
        => HexMap.CreateHexagon(radius).Count.Should().Be(count);

    [Fact]
    public void Rectangle_HasColumnsTimesRows_AndStraightEdges()
    {
        var map = HexMap.CreateRectangle(5, 4);

        map.Count.Should().Be(20);

        // Offset rows shift q so each row starts at the same screen column on the left.
        var layout = new HexLayout(HexOrientation.PointyTop, 10);
        var leftEdge = map.Hexes.GroupBy(h => h.R).Select(g => g.Min(h => layout.ToPixel(h).X)).ToList();
        leftEdge.Max().Should().BeLessThan(leftEdge.Min() + layout.Width);
    }

    [Fact]
    public void Rectangle_RejectsEmptySizes()
    {
        var act = () => HexMap.CreateRectangle(0, 3);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetAndGet_Terrain()
    {
        var map = new HexMap();

        map.TerrainAt(new Hex(1, 1)).Should().BeNull();
        map.Set(new Hex(1, 1), Terrain.Forest);
        map.Set(new Hex(1, 1), Terrain.Grass);

        map.TerrainAt(new Hex(1, 1)).Should().Be(Terrain.Grass);
        map.Count.Should().Be(1);
        map.Remove(new Hex(1, 1)).Should().BeTrue();
        map.Contains(new Hex(1, 1)).Should().BeFalse();
    }

    [Fact]
    public void Bounds_CoverAllHexes_AndEmptyMapHasNone()
    {
        new HexMap().Bounds().Should().BeNull();

        var bounds = HexMap.CreateHexagon(2).Bounds();

        bounds.Should().Be((-2, 2, -2, 2));
    }

    [Theory]
    [InlineData(Terrain.Floor, 1, true, false)]
    [InlineData(Terrain.Grass, 1, true, false)]
    [InlineData(Terrain.Forest, 2, true, false)]
    [InlineData(Terrain.Rubble, 2, true, false)]
    [InlineData(Terrain.DeepWater, 2, true, false)]
    [InlineData(Terrain.Wall, int.MaxValue, false, true)]
    [InlineData(Terrain.Pit, int.MaxValue, false, false)]
    public void TerrainRules_MatchDesign(Terrain terrain, int cost, bool passable, bool blocksSight)
    {
        TerrainRules.MoveCost(terrain).Should().Be(cost);
        TerrainRules.IsPassable(terrain).Should().Be(passable);
        TerrainRules.BlocksSight(terrain).Should().Be(blocksSight);
    }

    [Fact]
    public void OffMapHexes_AreImpassableAndBlockSight()
    {
        var map = HexMap.CreateHexagon(1);

        map.IsPassable(new Hex(5, 5)).Should().BeFalse();
        map.MoveCost(new Hex(5, 5)).Should().Be(TerrainRules.Impassable);
        map.BlocksSight(new Hex(5, 5)).Should().BeTrue();
    }
}

public class PathfinderTests
{
    [Fact]
    public void OpenGround_PathIsStraightAndCostsOnePerHex()
    {
        var map = HexMap.CreateHexagon(6);

        var result = Pathfinder.FindPath(map, new Hex(-3, 0), new Hex(3, 0));

        result!.Cost.Should().Be(6);
        result.Path.First().Should().Be(new Hex(-3, 0));
        result.Path.Last().Should().Be(new Hex(3, 0));
        result.Path.Should().HaveCount(7);
        result.CostFeet(map).Should().Be(30);
    }

    [Fact]
    public void PathSteps_AreAlwaysAdjacent()
    {
        var map = HexMap.CreateHexagon(6);

        var path = Pathfinder.FindPath(map, new Hex(-4, 2), new Hex(3, -5))!.Path;

        for (var i = 1; i < path.Count; i++)
            Hex.Distance(path[i - 1], path[i]).Should().Be(1);
    }

    [Fact]
    public void StartEqualsGoal_IsZeroCost()
    {
        var map = HexMap.CreateHexagon(2);

        var result = Pathfinder.FindPath(map, Hex.Origin, Hex.Origin);

        result!.Cost.Should().Be(0);
        result.Path.Should().Equal(Hex.Origin);
    }

    [Fact]
    public void Walls_AreWalkedAround()
    {
        var map = HexMap.CreateHexagon(5);
        foreach (var q in Enumerable.Range(-3, 7))
            map.Set(new Hex(q, 0), Terrain.Wall);
        map.Set(new Hex(4, 0), Terrain.Floor);

        var result = Pathfinder.FindPath(map, new Hex(0, -2), new Hex(0, 2));

        result.Should().NotBeNull();
        result!.Path.Should().NotContain(h => map.TerrainAt(h) == Terrain.Wall);
        result.Cost.Should().BeGreaterThan(4);
    }

    [Fact]
    public void FullyWalledOff_HasNoPath()
    {
        var map = HexMap.CreateHexagon(4);
        foreach (var hex in Hex.Origin.Ring(2))
            map.Set(hex, Terrain.Wall);

        Pathfinder.FindPath(map, Hex.Origin, new Hex(4, 0)).Should().BeNull();
    }

    [Fact]
    public void DifficultTerrain_IsAvoidedWhenACheaperDetourExists()
    {
        var map = HexMap.CreateHexagon(3);
        map.Set(new Hex(0, 0), Terrain.Mud);
        map.Set(new Hex(1, 0), Terrain.Mud);

        var result = Pathfinder.FindPath(map, new Hex(-1, 0), new Hex(2, 0));

        result!.Cost.Should().Be(4);
        result.Path.Should().NotContain([new Hex(0, 0), new Hex(1, 0)]);
    }

    [Fact]
    public void DifficultTerrain_IsCrossedWhenThereIsNoCheaperRoute()
    {
        var map = new HexMap();
        map.Set(new Hex(0, 0), Terrain.Floor);
        map.Set(new Hex(1, 0), Terrain.Mud);
        map.Set(new Hex(2, 0), Terrain.Floor);

        Pathfinder.FindPath(map, new Hex(0, 0), new Hex(2, 0))!.Cost.Should().Be(3);
    }

    [Fact]
    public void BlockedHexes_AreNotEntered_AndGoalCannotBeBlocked()
    {
        var map = HexMap.CreateHexagon(3);
        var enemy = new Hex(1, 0);

        var around = Pathfinder.FindPath(map, new Hex(0, 0), new Hex(2, 0), h => h == enemy);
        around!.Path.Should().NotContain(enemy);

        Pathfinder.FindPath(map, new Hex(0, 0), enemy, h => h == enemy).Should().BeNull();
    }

    [Fact]
    public void StartOrGoalOffMap_HasNoPath()
    {
        var map = HexMap.CreateHexagon(2);

        Pathfinder.FindPath(map, new Hex(9, 9), Hex.Origin).Should().BeNull();
        Pathfinder.FindPath(map, Hex.Origin, new Hex(9, 9)).Should().BeNull();
    }

    [Fact]
    public void Reachable_OnOpenGround_IsAFilledHexagon()
    {
        var map = HexMap.CreateHexagon(8);

        var reachable = Pathfinder.Reachable(map, Hex.Origin, 3);

        reachable.Should().HaveCount(37);
        reachable[Hex.Origin].Should().Be(0);
        reachable[new Hex(3, 0)].Should().Be(3);
    }

    [Fact]
    public void Reachable_RespectsDifficultTerrainAndWalls()
    {
        var map = HexMap.CreateHexagon(4);
        map.Set(new Hex(1, 0), Terrain.Mud);
        map.Set(new Hex(0, 1), Terrain.Wall);

        var reachable = Pathfinder.Reachable(map, Hex.Origin, 2);

        reachable[new Hex(1, 0)].Should().Be(2);
        reachable.Should().NotContainKey(new Hex(2, 0));
        reachable.Should().NotContainKey(new Hex(0, 1));
        reachable.Values.Should().OnlyContain(c => c <= 2);
    }

    [Fact]
    public void Reachable_ZeroBudget_IsJustTheStart()
        => Pathfinder.Reachable(HexMap.CreateHexagon(3), Hex.Origin, 0).Keys.Should().Equal(Hex.Origin);

    [Fact]
    public void Reachable_AgreesWithFindPathCosts()
    {
        var map = HexMap.CreateHexagon(5);
        map.Set(new Hex(1, 0), Terrain.Forest);
        map.Set(new Hex(2, -1), Terrain.Wall);
        map.Set(new Hex(0, 2), Terrain.Rubble);

        foreach (var (hex, cost) in Pathfinder.Reachable(map, Hex.Origin, 6))
            Pathfinder.FindPath(map, Hex.Origin, hex)!.Cost.Should().Be(cost);
    }
}

public class LineOfSightTests
{
    [Fact]
    public void OpenGround_IsClear()
        => LineOfSight.HasClearLine(HexMap.CreateHexagon(6), new Hex(-4, 0), new Hex(4, 0)).Should().BeTrue();

    [Fact]
    public void WallInTheWay_Blocks()
    {
        var map = HexMap.CreateHexagon(6);
        map.Set(new Hex(0, 0), Terrain.Wall);

        LineOfSight.HasClearLine(map, new Hex(-4, 0), new Hex(4, 0)).Should().BeFalse();
    }

    [Fact]
    public void WallBesideTheLine_DoesNotBlock()
    {
        var map = HexMap.CreateHexagon(6);
        map.Set(new Hex(0, 2), Terrain.Wall);

        LineOfSight.HasClearLine(map, new Hex(-4, 0), new Hex(4, 0)).Should().BeTrue();
    }

    [Fact]
    public void TheEndHexesNeverBlock()
    {
        var map = HexMap.CreateHexagon(3);
        map.Set(new Hex(2, 0), Terrain.Wall);

        LineOfSight.HasClearLine(map, Hex.Origin, new Hex(2, 0)).Should().BeTrue();
    }

    [Fact]
    public void OffMapHexes_BlockSight()
    {
        var map = new HexMap();
        map.Set(new Hex(0, 0), Terrain.Floor);
        map.Set(new Hex(2, 0), Terrain.Floor);

        LineOfSight.HasClearLine(map, new Hex(0, 0), new Hex(2, 0)).Should().BeFalse();
    }

    [Fact]
    public void ForestAndWater_DoNotBlockSight()
    {
        var map = HexMap.CreateHexagon(4);
        map.Set(new Hex(1, 0), Terrain.Forest);
        map.Set(new Hex(2, 0), Terrain.DeepWater);

        LineOfSight.HasClearLine(map, Hex.Origin, new Hex(3, 0)).Should().BeTrue();
    }

    [Fact]
    public void Visible_ExcludesHexesBehindWalls()
    {
        var map = HexMap.CreateHexagon(5);
        map.Set(new Hex(1, 0), Terrain.Wall);

        var visible = LineOfSight.Visible(map, Hex.Origin, 4);

        visible.Should().Contain(Hex.Origin);
        visible.Should().Contain(new Hex(1, 0));
        visible.Should().NotContain(new Hex(3, 0));
        visible.Should().Contain(new Hex(-3, 0));
    }

    [Fact]
    public void IsSymmetric_ForCommonCases()
    {
        var map = HexMap.CreateHexagon(6);
        map.Set(new Hex(1, -1), Terrain.Wall);
        map.Set(new Hex(-2, 2), Terrain.Wall);

        foreach (var a in new[] { new Hex(-3, 1), new Hex(3, -1), new Hex(0, 3) })
        foreach (var b in new[] { new Hex(3, -3), new Hex(-4, 4), new Hex(2, 1) })
            LineOfSight.HasClearLine(map, a, b).Should().Be(LineOfSight.HasClearLine(map, b, a), $"{a} <-> {b}");
    }
}
