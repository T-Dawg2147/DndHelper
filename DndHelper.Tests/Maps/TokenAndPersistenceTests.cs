using DndHelper.Maps.Hexes;
using DndHelper.Maps.Maps;
using DndHelper.Maps.Navigation;
using DndHelper.Maps.Persistence;
using DndHelper.Maps.Tokens;
using FluentAssertions;

namespace DndHelper.Tests.Maps;

public class TokenTests
{
    [Theory]
    [InlineData("Wolf Rider", "WR")]
    [InlineData("Goblin", "Go")]
    [InlineData("Goblin 2", "G2")]
    [InlineData("Ann-Marie Cole", "AM")]
    [InlineData("X", "X")]
    [InlineData("", "?")]
    [InlineData("   ", "?")]
    public void Initials_AreShortLabels(string name, string expected)
        => new MapToken { Name = name }.Initials.Should().Be(expected);

    [Theory]
    [InlineData("Goblin", new string[0], "Goblin")]
    [InlineData("Goblin", new[] { "Goblin" }, "Goblin 2")]
    [InlineData("Goblin", new[] { "goblin", "Goblin 2" }, "Goblin 3")]
    [InlineData("  ", new string[0], "Token")]
    public void UniqueNames_AreNumbered(string name, string[] existing, string expected)
        => TokenNaming.Unique(name, existing).Should().Be(expected);

    [Fact]
    public void FreeHex_IsTheClosestUnoccupiedPassableOne()
    {
        var map = HexMap.CreateHexagon(3);
        map.Set(new Hex(1, 0), Terrain.Wall);
        var occupied = new HashSet<Hex> { Hex.Origin };

        var free = TokenPlacement.FindFreeHex(map, occupied, Hex.Origin);

        free.Should().NotBeNull();
        free!.Value.DistanceTo(Hex.Origin).Should().Be(1);
        free.Value.Should().NotBe(new Hex(1, 0));
    }

    [Fact]
    public void FreeHex_IsDeterministic()
    {
        var map = HexMap.CreateHexagon(3);

        TokenPlacement.FindFreeHex(map, new HashSet<Hex>(), Hex.Origin).Should().Be(Hex.Origin);
        TokenPlacement.FindFreeHex(map, new HashSet<Hex> { Hex.Origin }, Hex.Origin)
            .Should().Be(TokenPlacement.FindFreeHex(map, new HashSet<Hex> { Hex.Origin }, Hex.Origin));
    }

    [Fact]
    public void FreeHex_IsNullWhenNothingIsFree()
    {
        var map = HexMap.CreateHexagon(0);

        TokenPlacement.FindFreeHex(map, new HashSet<Hex> { Hex.Origin }, Hex.Origin).Should().BeNull();
    }
}

public class MovementRulesTests
{
    private static MapToken Token(string name, Hex hex, int speed = 30) => new() { Name = name, Hex = hex, SpeedFeet = speed };

    [Fact]
    public void Reachable_UsesSpeedDividedByHexSize()
    {
        var map = HexMap.CreateHexagon(10);
        var token = Token("A", Hex.Origin, speed: 30);

        var reach = MovementRules.Reachable(map, token, [token]);

        reach.Should().HaveCount(Hex.Origin.Spiral(6).Count());
        reach.Keys.Should().OnlyContain(h => h.DistanceTo(Hex.Origin) <= 6);
    }

    [Fact]
    public void Reachable_RespectsMapScale()
    {
        var map = HexMap.CreateHexagon(10);
        map.FeetPerHex = 10;
        var token = Token("A", Hex.Origin, speed: 30);

        MovementRules.Reachable(map, token, [token]).Keys.Should().OnlyContain(h => h.DistanceTo(Hex.Origin) <= 3);
    }

    [Fact]
    public void OtherTokens_BlockMovement_ButNotTheMoverItself()
    {
        var map = HexMap.CreateHexagon(5);
        var mover = Token("A", new Hex(0, 0));
        var blocker = Token("B", new Hex(1, 0));

        var reach = MovementRules.Reachable(map, mover, [mover, blocker]);

        reach.Should().NotContainKey(new Hex(1, 0));
        reach.Should().ContainKey(new Hex(0, 0));
    }

    [Fact]
    public void PathTo_IsNullBeyondSpeed()
    {
        var map = HexMap.CreateHexagon(10);
        var token = Token("A", Hex.Origin, speed: 15);

        MovementRules.PathTo(map, token, new Hex(3, 0), [token]).Should().NotBeNull();
        MovementRules.PathTo(map, token, new Hex(4, 0), [token]).Should().BeNull();
    }

    [Fact]
    public void DifficultTerrain_UsesUpMovement()
    {
        var map = HexMap.CreateHexagon(5);
        map.Set(new Hex(1, 0), Terrain.Mud);
        map.Set(new Hex(1, -1), Terrain.Mud);
        map.Set(new Hex(0, 1), Terrain.Mud);
        map.Set(new Hex(-1, 1), Terrain.Mud);
        map.Set(new Hex(-1, 0), Terrain.Mud);
        map.Set(new Hex(0, -1), Terrain.Mud);
        var token = Token("A", Hex.Origin, speed: 10);

        var reach = MovementRules.Reachable(map, token, [token]);

        reach.Should().HaveCount(7);
        reach[new Hex(1, 0)].Should().Be(2);
    }

    [Fact]
    public void TryMove_MovesAndReportsFeet()
    {
        var map = HexMap.CreateHexagon(6);
        var token = Token("A", Hex.Origin, speed: 30);

        MovementRules.TryMove(map, token, new Hex(4, 0), [token], out var feet).Should().BeTrue();

        token.Hex.Should().Be(new Hex(4, 0));
        feet.Should().Be(20);
    }

    [Fact]
    public void TryMove_RefusesOccupiedUnreachableAndSameHex()
    {
        var map = HexMap.CreateHexagon(10);
        var token = Token("A", Hex.Origin, speed: 10);
        var other = Token("B", new Hex(1, 0));

        MovementRules.TryMove(map, token, new Hex(1, 0), [token, other], out _).Should().BeFalse();
        MovementRules.TryMove(map, token, new Hex(5, 0), [token, other], out _).Should().BeFalse();
        MovementRules.TryMove(map, token, Hex.Origin, [token, other], out _).Should().BeFalse();
        token.Hex.Should().Be(Hex.Origin);
    }

    [Fact]
    public void ZeroSpeed_CannotMove()
    {
        var map = HexMap.CreateHexagon(3);
        var token = Token("Grappled", Hex.Origin, speed: 0);

        MovementRules.Reachable(map, token, [token]).Keys.Should().Equal(Hex.Origin);
    }
}

public class MapSerializerTests
{
    private static (HexMap Map, List<MapToken> Tokens) Sample()
    {
        var map = HexMap.CreateHexagon(2, Terrain.Grass);
        map.Name = "Ambush";
        map.FeetPerHex = 10;
        map.Set(new Hex(1, 0), Terrain.Wall);
        map.Set(new Hex(0, 1), Terrain.ShallowWater);
        var tokens = new List<MapToken>
        {
            new() { Name = "Wolf Rider", Hex = new Hex(-1, 1), Team = TokenTeam.Enemy, SpeedFeet = 40 },
            new() { Name = "Mira", Hex = new Hex(0, -1), Team = TokenTeam.Player, SpeedFeet = 30 }
        };
        return (map, tokens);
    }

    [Fact]
    public void RoundTrip_PreservesMapAndTokens()
    {
        var (map, tokens) = Sample();

        var loaded = MapSerializer.Deserialize(MapSerializer.Serialize(map, tokens));

        loaded.Map.Name.Should().Be("Ambush");
        loaded.Map.FeetPerHex.Should().Be(10);
        loaded.Map.Count.Should().Be(map.Count);
        loaded.Map.Cells.Should().BeEquivalentTo(map.Cells);
        loaded.Map.TerrainAt(new Hex(1, 0)).Should().Be(Terrain.Wall);

        loaded.Tokens.Should().HaveCount(2);
        loaded.Tokens[0].Id.Should().Be(tokens[0].Id);
        loaded.Tokens[0].Name.Should().Be("Wolf Rider");
        loaded.Tokens[0].Hex.Should().Be(new Hex(-1, 1));
        loaded.Tokens[0].Team.Should().Be(TokenTeam.Enemy);
        loaded.Tokens[0].SpeedFeet.Should().Be(40);
    }

    [Fact]
    public void Output_IsReadableJson_WithNamedTerrain()
    {
        var (map, tokens) = Sample();

        var json = MapSerializer.Serialize(map, tokens);

        json.Should().Contain("\"Terrain\": \"Wall\"").And.Contain("\"Version\": 1").And.Contain("\"Team\": \"Enemy\"");
        json.Should().NotContain("\"S\"");
    }

    [Fact]
    public void Output_IsDeterministic()
    {
        var (map, tokens) = Sample();

        MapSerializer.Serialize(map, tokens).Should().Be(MapSerializer.Serialize(map, tokens));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("{ \"Version\": 1, \"Cells\": [ { \"Q\": 0, \"R\": 0, \"Terrain\": \"Lava\" } ] }")]
    [InlineData("{ \"Version\": 1, \"Cells\": [ { \"Q\": 0, \"R\": 0, \"Terrain\": 99 } ] }")]
    public void InvalidFiles_AreRejectedWithAFormatException(string json)
    {
        var act = () => MapSerializer.Deserialize(json);

        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    public void UnsupportedVersions_AreRejected(int version)
    {
        var act = () => MapSerializer.Deserialize($"{{ \"Version\": {version} }}");

        act.Should().Throw<FormatException>().WithMessage("*version*");
    }

    [Fact]
    public void TooManyHexes_AreRejected()
    {
        var cells = string.Join(",", Enumerable.Range(0, MapSerializer.MaxCells + 1).Select(i => $"{{\"Q\":{i},\"R\":0,\"Terrain\":\"Floor\"}}"));

        var act = () => MapSerializer.Deserialize($"{{ \"Version\": 1, \"Cells\": [{cells}] }}");

        act.Should().Throw<FormatException>().WithMessage("*too many hexes*");
    }

    [Fact]
    public void TooManyTokens_AreRejected()
    {
        var tokens = string.Join(",", Enumerable.Range(0, MapSerializer.MaxTokens + 1).Select(i => $"{{\"Name\":\"T{i}\"}}"));

        var act = () => MapSerializer.Deserialize($"{{ \"Version\": 1, \"Tokens\": [{tokens}] }}");

        act.Should().Throw<FormatException>().WithMessage("*too many tokens*");
    }

    [Fact]
    public void OddValues_AreCleanedUp()
    {
        const string json = """
        {
          "Version": 1, "Name": "   ", "FeetPerHex": 9999,
          "Cells": [ { "Q": 0, "R": 0, "Terrain": "Floor" } ],
          "Tokens": [ { "Id": "", "Name": " ", "Team": 42, "SpeedFeet": -50, "Hex": { "Q": 3, "R": -3 } } ]
        }
        """;

        var loaded = MapSerializer.Deserialize(json);

        loaded.Map.Name.Should().Be("Untitled map");
        loaded.Map.FeetPerHex.Should().Be(100);

        var token = loaded.Tokens.Single();
        token.Id.Should().NotBeNullOrWhiteSpace();
        token.Name.Should().Be("Token");
        token.Team.Should().Be(TokenTeam.Neutral);
        token.SpeedFeet.Should().Be(0);
        token.Hex.Should().Be(new Hex(3, -3));
    }

    [Fact]
    public void EmptyMap_RoundTrips()
    {
        var loaded = MapSerializer.Deserialize(MapSerializer.Serialize(new HexMap(), []));

        loaded.Map.Count.Should().Be(0);
        loaded.Tokens.Should().BeEmpty();
    }
}
