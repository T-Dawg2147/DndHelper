using DndHelper.Maps.Hexes;

namespace DndHelper.Maps.Areas;

public enum AreaShapeKind
{
    /// <summary>Sphere, emanation or radius: everything within the size of the origin hex.</summary>
    Sphere,

    /// <summary>Cylinder; on a flat map it covers the same hexes as a sphere.</summary>
    Cylinder,

    /// <summary>Cone whose width at any point equals its distance from the origin (5e rules).</summary>
    Cone,

    /// <summary>Line of a given length and width running from the origin towards the aim hex.</summary>
    Line,

    /// <summary>Cube, with one face touching the origin and extending towards the aim hex.</summary>
    Cube
}

/// <summary>A spell or effect area: its shape, size and aim, ready to be turned into covered hexes.</summary>
public sealed record AreaTemplate(AreaShapeKind Kind, Hex Origin, Hex Aim, int SizeFeet, int WidthFeet = 5)
{
    /// <summary>Maps a spell's shape text (e.g. "cone", "radius", "line") to a kind. Unknown text returns null.</summary>
    public static AreaShapeKind? ParseKind(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        return text.Trim().ToLowerInvariant() switch
        {
            "sphere" or "radius" or "emanation" => AreaShapeKind.Sphere,
            "cylinder" => AreaShapeKind.Cylinder,
            "cone" => AreaShapeKind.Cone,
            "line" => AreaShapeKind.Line,
            "cube" or "square" => AreaShapeKind.Cube,
            _ => null
        };
    }
}

/// <summary>
/// Works out which hexes an <see cref="AreaTemplate"/> covers. Shapes are measured in feet from hex centres,
/// so they behave the same in both hex orientations. A hex is covered when its centre is inside the shape
/// (touching the edge counts, which is the generous reading at the table).
/// </summary>
public static class AreaShapes
{
    private const double Epsilon = 1e-6;

    // Cone half-angle: width equals length, so tan(half) = 0.5.
    private static readonly double ConeHalfAngle = Math.Atan(0.5);

    public static IReadOnlySet<Hex> Cover(
        AreaTemplate area,
        int feetPerHex = 5,
        HexOrientation orientation = HexOrientation.PointyTop)
    {
        if (area.SizeFeet <= 0 || feetPerHex <= 0) return new HashSet<Hex>();

        return area.Kind switch
        {
            AreaShapeKind.Sphere or AreaShapeKind.Cylinder => Radius(area, feetPerHex),
            AreaShapeKind.Cone => Directional(area, feetPerHex, orientation, InCone),
            AreaShapeKind.Line => Directional(area, feetPerHex, orientation, InLine),
            AreaShapeKind.Cube => Directional(area, feetPerHex, orientation, InCube),
            _ => new HashSet<Hex>()
        };
    }

    private static HashSet<Hex> Radius(AreaTemplate area, int feetPerHex)
    {
        var radius = area.SizeFeet / feetPerHex;
        return area.Origin.Spiral(radius).ToHashSet();
    }

    private delegate bool Test(double forward, double lateral, double length, double width);

    private static HashSet<Hex> Directional(AreaTemplate area, int feetPerHex, HexOrientation orientation, Test test)
    {
        // Layout whose neighbouring hex centres are exactly feetPerHex apart, so pixel units are feet.
        var layout = new HexLayout(orientation, 1);
        var scale = feetPerHex / layout.Width;
        if (orientation == HexOrientation.FlatTop) scale = feetPerHex / layout.Height;

        var origin = Scaled(layout.ToPixel(area.Origin), scale);
        var aim = Scaled(layout.ToPixel(area.Aim), scale);

        var dx = aim.X - origin.X;
        var dy = aim.Y - origin.Y;
        var norm = Math.Sqrt(dx * dx + dy * dy);

        // Aiming at the origin itself defaults to east.
        var (ux, uy) = norm < Epsilon ? (1.0, 0.0) : (dx / norm, dy / norm);

        var reach = area.SizeFeet / feetPerHex + 1;
        var result = new HashSet<Hex>();

        foreach (var hex in area.Origin.Spiral(reach))
        {
            if (hex == area.Origin) continue;

            var p = Scaled(layout.ToPixel(hex), scale);
            var vx = p.X - origin.X;
            var vy = p.Y - origin.Y;
            var forward = vx * ux + vy * uy;
            var lateral = -vx * uy + vy * ux;

            if (test(forward, lateral, area.SizeFeet, area.WidthFeet))
                result.Add(hex);
        }

        return result;
    }

    private static bool InCone(double forward, double lateral, double length, double width)
    {
        if (forward <= Epsilon || forward > length + Epsilon) return false;
        var distance = Math.Sqrt(forward * forward + lateral * lateral);
        if (distance > length + Epsilon) return false;
        var angle = Math.Atan2(Math.Abs(lateral), forward);
        return angle <= ConeHalfAngle + Epsilon;
    }

    private static bool InLine(double forward, double lateral, double length, double width)
        => forward > Epsilon && forward <= length + Epsilon && Math.Abs(lateral) <= width / 2 + Epsilon;

    private static bool InCube(double forward, double lateral, double length, double width)
        => forward > Epsilon && forward <= length + Epsilon && Math.Abs(lateral) <= length / 2 + Epsilon;

    private static Point2 Scaled(Point2 p, double scale) => new(p.X * scale, p.Y * scale);
}
