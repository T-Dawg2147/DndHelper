namespace DndHelper.Maps.Hexes;

public enum HexOrientation
{
    PointyTop,
    FlatTop
}

public readonly record struct Point2(double X, double Y);

/// <summary>
/// Converts between hex coordinates and screen pixels. Both orientations are supported;
/// the app currently uses pointy-top. Has no UI dependency, so it is fully testable.
/// </summary>
public sealed class HexLayout(HexOrientation orientation, double size, Point2 origin = default)
{
    private static readonly double Sqrt3 = Math.Sqrt(3);

    public HexOrientation Orientation { get; } = orientation;

    /// <summary>Distance from a hex's centre to any corner, in pixels.</summary>
    public double Size { get; } = size > 0 ? size : throw new ArgumentOutOfRangeException(nameof(size));

    /// <summary>Pixel position of hex (0, 0).</summary>
    public Point2 Origin { get; } = origin;

    public double Width => Orientation == HexOrientation.PointyTop ? Sqrt3 * Size : 2 * Size;
    public double Height => Orientation == HexOrientation.PointyTop ? 2 * Size : Sqrt3 * Size;

    public HexLayout With(double? size = null, Point2? origin = null, HexOrientation? orientation = null)
        => new(orientation ?? Orientation, size ?? Size, origin ?? Origin);

    public Point2 ToPixel(Hex hex)
    {
        var (x, y) = Orientation == HexOrientation.PointyTop
            ? (Sqrt3 * hex.Q + Sqrt3 / 2 * hex.R, 1.5 * hex.R)
            : (1.5 * hex.Q, Sqrt3 / 2 * hex.Q + Sqrt3 * hex.R);

        return new Point2(x * Size + Origin.X, y * Size + Origin.Y);
    }

    public Hex FromPixel(Point2 point)
    {
        var x = (point.X - Origin.X) / Size;
        var y = (point.Y - Origin.Y) / Size;

        var (q, r) = Orientation == HexOrientation.PointyTop
            ? (Sqrt3 / 3 * x - y / 3, 2.0 / 3 * y)
            : (2.0 / 3 * x, -x / 3 + Sqrt3 / 3 * y);

        return Hex.Round(q, r);
    }

    /// <summary>The six corner points of a hex, in order around its outline.</summary>
    public IReadOnlyList<Point2> Corners(Hex hex)
    {
        var centre = ToPixel(hex);
        var start = Orientation == HexOrientation.PointyTop ? 30.0 : 0.0;
        var corners = new Point2[6];

        for (var i = 0; i < 6; i++)
        {
            var angle = (60 * i + start) * Math.PI / 180;
            corners[i] = new Point2(centre.X + Size * Math.Cos(angle), centre.Y + Size * Math.Sin(angle));
        }

        return corners;
    }
}
