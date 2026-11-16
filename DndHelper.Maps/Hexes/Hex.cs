using System.Text.Json.Serialization;

namespace DndHelper.Maps.Hexes;

/// <summary>A hex in axial coordinates (q = column, r = row). The third cube coordinate, s, is derived.</summary>
public readonly record struct Hex(int Q, int R)
{
    /// <summary>The six neighbour offsets, counter-clockwise starting east (pointy-top: E, NE, NW, W, SW, SE).</summary>
    public static readonly IReadOnlyList<Hex> Directions =
        [new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1)];

    public static Hex Origin => new(0, 0);

    [JsonIgnore]
    public int S => -Q - R;

    public static Hex operator +(Hex a, Hex b) => new(a.Q + b.Q, a.R + b.R);
    public static Hex operator -(Hex a, Hex b) => new(a.Q - b.Q, a.R - b.R);
    public static Hex operator *(Hex a, int k) => new(a.Q * k, a.R * k);

    /// <summary>Number of hex steps between two hexes.</summary>
    public static int Distance(Hex a, Hex b)
    {
        var d = a - b;
        return (Math.Abs(d.Q) + Math.Abs(d.R) + Math.Abs(d.Q + d.R)) / 2;
    }

    public int DistanceTo(Hex other) => Distance(this, other);

    /// <summary>Distance in feet, at 5 ft. per hex by default.</summary>
    public int DistanceFeetTo(Hex other, int feetPerHex = 5) => Distance(this, other) * feetPerHex;

    public Hex Neighbor(int direction) => this + Directions[((direction % 6) + 6) % 6];

    public IEnumerable<Hex> Neighbors()
    {
        foreach (var direction in Directions)
            yield return this + direction;
    }

    /// <summary>All hexes exactly <paramref name="radius"/> steps away (empty for a negative radius; just this hex for 0).</summary>
    public IEnumerable<Hex> Ring(int radius)
    {
        if (radius < 0) yield break;
        if (radius == 0)
        {
            yield return this;
            yield break;
        }

        var hex = this + Directions[4] * radius;
        for (var side = 0; side < 6; side++)
        {
            for (var step = 0; step < radius; step++)
            {
                yield return hex;
                hex = hex.Neighbor(side);
            }
        }
    }

    /// <summary>All hexes within <paramref name="radius"/> steps, including this one, closest first.</summary>
    public IEnumerable<Hex> Spiral(int radius)
    {
        for (var r = 0; r <= radius; r++)
        {
            foreach (var hex in Ring(r))
                yield return hex;
        }
    }

    /// <summary>Rotates around <paramref name="center"/> by 60 degrees clockwise, repeated <paramref name="turns"/> times (negative turns go anticlockwise).</summary>
    public Hex RotateAround(Hex center, int turns)
    {
        var offset = this - center;
        var steps = ((turns % 6) + 6) % 6;

        for (var i = 0; i < steps; i++)
            offset = new Hex(-offset.R, -offset.S);

        return center + offset;
    }

    /// <summary>The hexes on a straight line from <paramref name="a"/> to <paramref name="b"/>, both included.</summary>
    public static IReadOnlyList<Hex> Line(Hex a, Hex b)
    {
        var n = Distance(a, b);
        if (n == 0) return [a];

        // A tiny nudge breaks ties consistently when the line runs exactly along a hex edge.
        const double nudge = 1e-6;
        var result = new List<Hex>(n + 1);

        for (var i = 0; i <= n; i++)
        {
            var t = (double)i / n;
            var q = Lerp(a.Q + nudge, b.Q + nudge, t);
            var r = Lerp(a.R + 2 * nudge, b.R + 2 * nudge, t);
            result.Add(Round(q, r));
        }

        return result;
    }

    /// <summary>Rounds fractional axial coordinates to the nearest hex.</summary>
    public static Hex Round(double q, double r)
    {
        var s = -q - r;
        var rq = Math.Round(q);
        var rr = Math.Round(r);
        var rs = Math.Round(s);

        var dq = Math.Abs(rq - q);
        var dr = Math.Abs(rr - r);
        var ds = Math.Abs(rs - s);

        if (dq > dr && dq > ds) rq = -rr - rs;
        else if (dr > ds) rr = -rq - rs;

        return new Hex((int)rq, (int)rr);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    public override string ToString() => $"({Q}, {R})";
}
