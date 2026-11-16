using DndHelper.Maps.Hexes;
using FluentAssertions;

namespace DndHelper.Tests.Maps;

public class HexTests
{
    [Fact]
    public void CubeCoordinates_AlwaysSumToZero()
    {
        var hex = new Hex(3, -5);

        (hex.Q + hex.R + hex.S).Should().Be(0);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(0, 0, 1, 0, 1)]
    [InlineData(0, 0, 1, -1, 1)]
    [InlineData(0, 0, 1, 1, 2)]
    [InlineData(0, 0, 3, -3, 3)]
    [InlineData(-2, 1, 2, -1, 4)]
    [InlineData(2, -3, -1, 4, 7)]
    public void Distance_CountsSteps(int aq, int ar, int bq, int br, int expected)
    {
        Hex.Distance(new Hex(aq, ar), new Hex(bq, br)).Should().Be(expected);
        Hex.Distance(new Hex(bq, br), new Hex(aq, ar)).Should().Be(expected);
    }

    [Fact]
    public void DistanceFeet_UsesFiveFeetPerHexByDefault()
    {
        Hex.Origin.DistanceFeetTo(new Hex(4, 0)).Should().Be(20);
        Hex.Origin.DistanceFeetTo(new Hex(4, 0), 10).Should().Be(40);
    }

    [Fact]
    public void Neighbors_AreSixHexesAtDistanceOne()
    {
        var hex = new Hex(2, -1);

        var neighbors = hex.Neighbors().ToList();

        neighbors.Should().HaveCount(6).And.OnlyHaveUniqueItems();
        neighbors.Should().OnlyContain(n => Hex.Distance(hex, n) == 1);
    }

    [Fact]
    public void Neighbor_WrapsDirectionIndex()
    {
        Hex.Origin.Neighbor(6).Should().Be(Hex.Origin.Neighbor(0));
        Hex.Origin.Neighbor(-1).Should().Be(Hex.Origin.Neighbor(5));
    }

    [Fact]
    public void Neighbor_Opposites_CancelOut()
    {
        for (var i = 0; i < 6; i++)
            Hex.Origin.Neighbor(i).Should().Be(Hex.Origin.Neighbor(i + 3) * -1);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 6)]
    [InlineData(2, 12)]
    [InlineData(5, 30)]
    public void Ring_HasSixTimesRadiusHexes_AllAtThatDistance(int radius, int count)
    {
        var centre = new Hex(3, -2);

        var ring = centre.Ring(radius).ToList();

        ring.Should().HaveCount(count).And.OnlyHaveUniqueItems();
        ring.Should().OnlyContain(h => Hex.Distance(centre, h) == radius);
    }

    [Fact]
    public void Ring_NegativeRadius_IsEmpty()
        => Hex.Origin.Ring(-1).Should().BeEmpty();

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 7)]
    [InlineData(2, 19)]
    [InlineData(4, 61)]
    public void Spiral_CoversEveryHexWithinRadiusOnce(int radius, int count)
    {
        var spiral = Hex.Origin.Spiral(radius).ToList();

        spiral.Should().HaveCount(count).And.OnlyHaveUniqueItems();
        spiral.First().Should().Be(Hex.Origin);
        spiral.Should().OnlyContain(h => Hex.Distance(Hex.Origin, h) <= radius);
    }

    [Fact]
    public void Rotate_SixTurnsReturnsToStart()
    {
        var hex = new Hex(3, -1);
        var centre = new Hex(1, 1);

        hex.RotateAround(centre, 6).Should().Be(hex);
        hex.RotateAround(centre, 1).RotateAround(centre, -1).Should().Be(hex);
    }

    [Fact]
    public void Rotate_KeepsDistanceToCentre()
    {
        var centre = new Hex(-2, 3);
        var hex = new Hex(2, 0);

        for (var turns = 0; turns < 6; turns++)
            Hex.Distance(centre, hex.RotateAround(centre, turns)).Should().Be(Hex.Distance(centre, hex));
    }

    [Fact]
    public void Rotate_OneTurnClockwise_MovesEastNeighbourToSouthEast()
        => new Hex(1, 0).RotateAround(Hex.Origin, 1).Should().Be(new Hex(0, 1));

    [Fact]
    public void Line_IncludesBothEnds_AndStepsByOne()
    {
        var a = new Hex(-3, 2);
        var b = new Hex(4, -4);

        var line = Hex.Line(a, b);

        line.Should().HaveCount(Hex.Distance(a, b) + 1);
        line.First().Should().Be(a);
        line.Last().Should().Be(b);

        for (var i = 1; i < line.Count; i++)
            Hex.Distance(line[i - 1], line[i]).Should().Be(1);
    }

    [Fact]
    public void Line_ToSelf_IsJustThatHex()
        => Hex.Line(new Hex(2, 2), new Hex(2, 2)).Should().Equal(new Hex(2, 2));

    [Fact]
    public void Line_AlongAnAxis_IsStraight()
        => Hex.Line(Hex.Origin, new Hex(3, 0)).Should().Equal(new Hex(0, 0), new Hex(1, 0), new Hex(2, 0), new Hex(3, 0));

    [Fact]
    public void Line_IsDeterministic_AndSymmetricInLength()
    {
        var a = new Hex(0, 0);
        var b = new Hex(5, -2);

        Hex.Line(a, b).Should().Equal(Hex.Line(a, b));
        Hex.Line(b, a).Should().HaveCount(Hex.Line(a, b).Count);
    }

    [Theory]
    [InlineData(0.1, 0.1, 0, 0)]
    [InlineData(0.9, 0.05, 1, 0)]
    [InlineData(-0.4, 1.3, 0, 1)]
    public void Round_PicksNearestHex(double q, double r, int expectedQ, int expectedR)
        => Hex.Round(q, r).Should().Be(new Hex(expectedQ, expectedR));
}

public class HexLayoutTests
{
    [Theory]
    [InlineData(HexOrientation.PointyTop)]
    [InlineData(HexOrientation.FlatTop)]
    public void PixelRoundTrip_ReturnsTheSameHex(HexOrientation orientation)
    {
        var layout = new HexLayout(orientation, 24, new Point2(100, -40));

        foreach (var hex in Hex.Origin.Spiral(6))
            layout.FromPixel(layout.ToPixel(hex)).Should().Be(hex);
    }

    [Theory]
    [InlineData(HexOrientation.PointyTop)]
    [InlineData(HexOrientation.FlatTop)]
    public void PixelsNearACentre_MapToThatHex(HexOrientation orientation)
    {
        var layout = new HexLayout(orientation, 30);
        var hex = new Hex(2, -3);
        var centre = layout.ToPixel(hex);

        layout.FromPixel(new Point2(centre.X + 5, centre.Y - 5)).Should().Be(hex);
    }

    [Fact]
    public void PointyTop_PlacesNeighboursInRowsAndOffsetColumns()
    {
        var layout = new HexLayout(HexOrientation.PointyTop, 10);

        var east = layout.ToPixel(new Hex(1, 0));
        var southEast = layout.ToPixel(new Hex(0, 1));

        east.X.Should().BeApproximately(layout.Width, 1e-9);
        east.Y.Should().BeApproximately(0, 1e-9);
        southEast.X.Should().BeApproximately(layout.Width / 2, 1e-9);
        southEast.Y.Should().BeApproximately(layout.Height * 0.75, 1e-9);
    }

    [Fact]
    public void FlatTop_PlacesNeighboursInColumnsAndOffsetRows()
    {
        var layout = new HexLayout(HexOrientation.FlatTop, 10);

        var south = layout.ToPixel(new Hex(0, 1));
        var northEast = layout.ToPixel(new Hex(1, -1));

        south.X.Should().BeApproximately(0, 1e-9);
        south.Y.Should().BeApproximately(layout.Height, 1e-9);
        northEast.X.Should().BeApproximately(layout.Width * 0.75, 1e-9);
        northEast.Y.Should().BeApproximately(-layout.Height / 2, 1e-9);
    }

    [Theory]
    [InlineData(HexOrientation.PointyTop)]
    [InlineData(HexOrientation.FlatTop)]
    public void Corners_AreSixPointsAtSizeFromCentre(HexOrientation orientation)
    {
        var layout = new HexLayout(orientation, 20, new Point2(7, 9));
        var hex = new Hex(1, 1);
        var centre = layout.ToPixel(hex);

        var corners = layout.Corners(hex);

        corners.Should().HaveCount(6);
        foreach (var corner in corners)
        {
            var distance = Math.Sqrt(Math.Pow(corner.X - centre.X, 2) + Math.Pow(corner.Y - centre.Y, 2));
            distance.Should().BeApproximately(20, 1e-9);
        }
    }

    [Fact]
    public void PointyTop_HasAPointAtTheTop_AndFlatTop_HasAFlatEdge()
    {
        var pointy = new HexLayout(HexOrientation.PointyTop, 10).Corners(Hex.Origin);
        var flat = new HexLayout(HexOrientation.FlatTop, 10).Corners(Hex.Origin);

        pointy.Count(c => Math.Abs(c.Y - pointy.Min(p => p.Y)) < 1e-9).Should().Be(1);
        flat.Count(c => Math.Abs(c.Y - flat.Min(p => p.Y)) < 1e-9).Should().Be(2);
    }

    [Fact]
    public void AdjacentHexes_ShareTwoCorners()
    {
        var layout = new HexLayout(HexOrientation.PointyTop, 10);

        var a = layout.Corners(Hex.Origin);
        var b = layout.Corners(new Hex(1, 0));

        var shared = a.Count(p => b.Any(q => Math.Abs(p.X - q.X) < 1e-6 && Math.Abs(p.Y - q.Y) < 1e-6));
        shared.Should().Be(2);
    }

    [Fact]
    public void SizeMustBePositive()
    {
        var act = () => new HexLayout(HexOrientation.PointyTop, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void With_ChangesOnlyWhatIsGiven()
    {
        var layout = new HexLayout(HexOrientation.PointyTop, 10, new Point2(1, 2));

        var zoomed = layout.With(size: 20);

        zoomed.Size.Should().Be(20);
        zoomed.Origin.Should().Be(new Point2(1, 2));
        zoomed.Orientation.Should().Be(HexOrientation.PointyTop);
        layout.With(orientation: HexOrientation.FlatTop).Orientation.Should().Be(HexOrientation.FlatTop);
    }
}
