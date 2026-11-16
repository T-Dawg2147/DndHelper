using DndHelper.Maps.Hexes;
using DndHelper.Maps.Maps;

namespace DndHelper.Maps.Generation;

public enum MapStyle
{
    Meadow,
    Forest,
    Cave,
    Dungeon,
    Crypt
}

/// <summary>What to generate. The same options (including the seed) always give the same map.</summary>
public sealed record GenerationOptions(MapStyle Style, int Columns, int Rows, int Seed);

/// <summary>
/// Builds playable maps. Every style keeps all walkable ground connected, so players can reach everything.
/// Everything is pure logic driven by a seeded random generator.
/// </summary>
public static class MapGenerator
{
    public const int MinSize = 8;
    public const int MaxSize = 80;

    public static HexMap Generate(GenerationOptions options)
    {
        var columns = Math.Clamp(options.Columns, MinSize, MaxSize);
        var rows = Math.Clamp(options.Rows, MinSize, MaxSize);
        var random = new Random(options.Seed);

        var map = options.Style switch
        {
            MapStyle.Meadow => Meadow(columns, rows, random),
            MapStyle.Forest => Forest(columns, rows, random),
            MapStyle.Cave => Cave(columns, rows, random),
            MapStyle.Dungeon => Dungeon(columns, rows, random),
            MapStyle.Crypt => Crypt(columns, rows, random),
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };

        map.Name = $"{options.Style} (seed {options.Seed})";
        return map;
    }

    /// <summary>Groups every walkable hex into connected regions, largest first.</summary>
    public static IReadOnlyList<IReadOnlyList<Hex>> Regions(HexMap map)
    {
        var seen = new HashSet<Hex>();
        var regions = new List<IReadOnlyList<Hex>>();

        foreach (var start in map.Hexes)
        {
            if (seen.Contains(start) || !map.IsPassable(start)) continue;

            var region = new List<Hex>();
            var queue = new Queue<Hex>();
            queue.Enqueue(start);
            seen.Add(start);

            while (queue.Count > 0)
            {
                var hex = queue.Dequeue();
                region.Add(hex);

                foreach (var next in hex.Neighbors())
                {
                    if (map.IsPassable(next) && seen.Add(next))
                        queue.Enqueue(next);
                }
            }

            regions.Add(region);
        }

        return regions.OrderByDescending(r => r.Count).ToList();
    }

    // ---- Outdoors ----

    private static HexMap Meadow(int columns, int rows, Random random)
    {
        var map = HexMap.CreateRectangle(columns, rows, Terrain.Grass);
        var area = map.Count;

        for (var i = 0; i < Math.Max(1, area / 140); i++)
            Blob(map, RandomCell(map, random), random.Next(1, 4), Terrain.Forest, 0.85, random);

        for (var i = 0; i < Math.Max(1, area / 220); i++)
            Blob(map, RandomCell(map, random), random.Next(1, 3), Terrain.Rubble, 0.7, random);

        for (var i = 0; i < Math.Max(1, area / 300); i++)
            Blob(map, RandomCell(map, random), random.Next(1, 3), Terrain.Mud, 0.8, random);

        if (random.NextDouble() < 0.8)
            Pond(map, RandomCell(map, random), random.Next(2, 4), random);

        return map;
    }

    private static HexMap Forest(int columns, int rows, Random random)
    {
        var map = HexMap.CreateRectangle(columns, rows, Terrain.Grass);
        var trees = new HashSet<Hex>(map.Hexes.Where(_ => random.NextDouble() < 0.48));

        for (var pass = 0; pass < 3; pass++)
        {
            var next = new HashSet<Hex>();
            foreach (var hex in map.Hexes)
            {
                var count = hex.Neighbors().Count(trees.Contains);
                var isTree = trees.Contains(hex);
                if (count >= 4 || (isTree && count == 3)) next.Add(hex);
            }

            trees = next;
        }

        foreach (var hex in trees) map.Set(hex, Terrain.Forest);

        // A winding stream gives the forest a landmark; it is shallow so it never cuts the map in two.
        var cell = RandomCell(map, random);
        for (var i = 0; i < (columns + rows) * 2; i++)
        {
            map.Set(cell, Terrain.ShallowWater);
            var next = cell.Neighbor(random.Next(0, 3) == 0 ? random.Next(6) : 0);
            if (map.Contains(next)) cell = next;
        }

        for (var i = 0; i < Math.Max(1, map.Count / 250); i++)
            Blob(map, RandomCell(map, random), random.Next(1, 3), Terrain.Rubble, 0.7, random);

        return map;
    }

    private static void Pond(HexMap map, Hex centre, int radius, Random random)
    {
        foreach (var hex in centre.Spiral(radius))
        {
            if (!map.Contains(hex)) continue;
            var distance = hex.DistanceTo(centre);
            if (distance == radius && random.NextDouble() < 0.3) continue;
            map.Set(hex, distance <= radius - 2 ? Terrain.DeepWater : Terrain.ShallowWater);
        }
    }

    private static void Blob(HexMap map, Hex centre, int radius, Terrain terrain, double density, Random random)
    {
        foreach (var hex in centre.Spiral(radius))
        {
            if (map.Contains(hex) && random.NextDouble() < density)
                map.Set(hex, terrain);
        }
    }

    // ---- Caves ----

    private static HexMap Cave(int columns, int rows, Random random)
    {
        HexMap? best = null;

        for (var attempt = 0; attempt < 12; attempt++)
        {
            var map = HexMap.CreateRectangle(columns, rows, Terrain.Wall);
            var walls = new HashSet<Hex>();

            foreach (var hex in map.Hexes)
            {
                var onEdge = hex.Neighbors().Any(n => !map.Contains(n));
                if (onEdge || random.NextDouble() < 0.46) walls.Add(hex);
            }

            for (var pass = 0; pass < 4; pass++)
            {
                var next = new HashSet<Hex>();
                foreach (var hex in map.Hexes)
                {
                    var count = hex.Neighbors().Count(n => !map.Contains(n) || walls.Contains(n));
                    var isWall = walls.Contains(hex);
                    if (count >= 4 || (isWall && count == 3)) next.Add(hex);
                }

                walls = next;
            }

            foreach (var hex in map.Hexes)
                map.Set(hex, walls.Contains(hex) ? Terrain.Wall : Terrain.Floor);

            KeepLargestRegion(map);

            best = map;
            var open = map.Hexes.Count(map.IsPassable);
            if (open >= map.Count * 0.25) break;
        }

        var result = best!;

        if (result.Hexes.Count(result.IsPassable) < result.Count * 0.25)
        {
            // Extremely rare: give up on the noise and open a plain cavern in the middle.
            foreach (var hex in CentreOf(result).Spiral(Math.Min(columns, rows) / 3))
            {
                if (result.Contains(hex) && hex.Neighbors().All(result.Contains)) result.Set(hex, Terrain.Floor);
            }

            KeepLargestRegion(result);
        }

        var floors = result.Hexes.Where(result.IsPassable).ToList();

        foreach (var hex in floors)
        {
            if (random.NextDouble() < 0.06) result.Set(hex, Terrain.Rubble);
        }

        if (floors.Count > 40 && random.NextDouble() < 0.7)
        {
            var centre = floors[random.Next(floors.Count)];
            foreach (var hex in centre.Spiral(2))
            {
                if (!result.Contains(hex) || !result.IsPassable(hex)) continue;
                result.Set(hex, hex.DistanceTo(centre) == 0 ? Terrain.DeepWater : Terrain.ShallowWater);
            }
        }

        return result;
    }

    private static void KeepLargestRegion(HexMap map)
    {
        var regions = Regions(map);
        foreach (var region in regions.Skip(1))
        {
            foreach (var hex in region) map.Set(hex, Terrain.Wall);
        }
    }

    // ---- Dungeons ----

    private sealed record Room(Hex Centre, int Radius);

    private static HexMap Dungeon(int columns, int rows, Random random)
    {
        var map = HexMap.CreateRectangle(columns, rows, Terrain.Wall);
        var rooms = new List<Room>();
        var target = Math.Max(3, map.Count / 70);

        for (var attempt = 0; attempt < target * 30 && rooms.Count < target; attempt++)
        {
            var radius = random.Next(2, 5);
            var centre = RandomCell(map, random);

            var fits = centre.Spiral(radius + 1).All(map.Contains);
            var clear = rooms.All(r => r.Centre.DistanceTo(centre) > r.Radius + radius + 2);
            if (fits && clear) rooms.Add(new Room(centre, radius));
        }

        if (rooms.Count == 0)
            rooms.Add(new Room(CentreOf(map), 2));

        foreach (var room in rooms)
        {
            foreach (var hex in room.Centre.Spiral(room.Radius)) map.Set(hex, Terrain.Floor);
        }

        ConnectRooms(map, rooms, random);

        foreach (var room in rooms)
        {
            if (room.Radius >= 3 && random.NextDouble() < 0.5) map.Set(room.Centre, Terrain.Wall);
            if (random.NextDouble() < 0.35)
                Blob(map, room.Centre + Hex.Directions[random.Next(6)], 1, Terrain.Rubble, 0.8, random,
                    onlyOn: Terrain.Floor);
        }

        // A pillar in a room's middle must never cut the room off from its corridors.
        KeepLargestRegion(map);
        return map;
    }

    private static void ConnectRooms(HexMap map, List<Room> rooms, Random random)
    {
        var connected = new List<Room> { rooms[0] };
        var pending = rooms.Skip(1).ToList();

        while (pending.Count > 0)
        {
            var (from, to) = pending
                .SelectMany(p => connected.Select(c => (From: c, To: p)))
                .MinBy(pair => pair.From.Centre.DistanceTo(pair.To.Centre));

            Corridor(map, from.Centre, to.Centre, random);
            connected.Add(to);
            pending.Remove(to);
        }

        for (var i = 0; i < rooms.Count / 4 && rooms.Count > 2; i++)
        {
            var a = rooms[random.Next(rooms.Count)];
            var b = rooms[random.Next(rooms.Count)];
            if (a != b) Corridor(map, a.Centre, b.Centre, random);
        }
    }

    private static void Corridor(HexMap map, Hex from, Hex to, Random random)
    {
        // Bend the corridor through a point near the middle so passages are not all dead straight.
        var mid = new Hex((from.Q + to.Q) / 2 + random.Next(-2, 3), (from.R + to.R) / 2 + random.Next(-2, 3));
        if (!map.Contains(mid)) mid = from;

        foreach (var hex in Hex.Line(from, mid).Concat(Hex.Line(mid, to)))
        {
            if (map.Contains(hex) && map.TerrainAt(hex) == Terrain.Wall)
                map.Set(hex, Terrain.Floor);
        }
    }

    // ---- Crypts ----

    private static HexMap Crypt(int columns, int rows, Random random)
    {
        var map = HexMap.CreateRectangle(columns, rows, Terrain.Wall);
        var bounds = map.Bounds()!.Value;
        var hallRow = (bounds.MinR + bounds.MaxR) / 2;

        var hall = map.Hexes
            .Where(h => h.R == hallRow || h.R == hallRow + 1)
            .OrderBy(h => h.Q)
            .ToList();

        var minQ = hall.Min(h => h.Q) + 2;
        var maxQ = hall.Max(h => h.Q) - 2;
        foreach (var hex in hall.Where(h => h.Q >= minQ && h.Q <= maxQ)) map.Set(hex, Terrain.Floor);

        // Pillars line one side of the hall only, so the other side always stays open.
        for (var q = minQ + 2; q <= maxQ - 2; q += 3)
        {
            var pillar = new Hex(q, hallRow);
            if (map.Contains(pillar)) map.Set(pillar, Terrain.Wall);
        }

        // Burial chambers above and below, each opening onto the hall.
        var spacing = 6;
        for (var q = minQ + 2; q <= maxQ - 2; q += spacing)
        {
            foreach (var side in new[] { -1, 1 })
            {
                var radius = random.Next(1, 3);
                var offset = side < 0 ? -(radius + 2) : radius + 3;
                var centre = new Hex(q - offset / 2, hallRow + offset);
                if (!centre.Spiral(radius + 1).All(map.Contains)) continue;
                if (random.NextDouble() < 0.15) continue;

                foreach (var hex in centre.Spiral(radius)) map.Set(hex, Terrain.Floor);

                foreach (var hex in Hex.Line(centre, side < 0 ? new Hex(q, hallRow) : new Hex(q, hallRow + 1)))
                {
                    if (map.Contains(hex) && map.TerrainAt(hex) == Terrain.Wall) map.Set(hex, Terrain.Floor);
                }

                if (random.NextDouble() < 0.5) map.Set(centre, Terrain.Rubble);
            }
        }

        // A grand chamber at the far end, with a pit as its centrepiece.
        var endCentre = new Hex(maxQ + 1, hallRow);
        if (endCentre.Spiral(4).All(map.Contains))
        {
            foreach (var hex in endCentre.Spiral(3)) map.Set(hex, Terrain.Floor);
            map.Set(endCentre + Hex.Directions[random.Next(6)], Terrain.Pit);
        }

        KeepLargestRegion(map);
        return map;
    }

    // ---- Helpers ----

    private static void Blob(HexMap map, Hex centre, int radius, Terrain terrain, double density, Random random, Terrain onlyOn)
    {
        foreach (var hex in centre.Spiral(radius))
        {
            if (map.Contains(hex) && map.TerrainAt(hex) == onlyOn && random.NextDouble() < density)
                map.Set(hex, terrain);
        }
    }

    private static Hex RandomCell(HexMap map, Random random)
        => map.Hexes.ElementAt(random.Next(map.Count));

    private static Hex CentreOf(HexMap map)
    {
        var b = map.Bounds()!.Value;
        var target = new Hex((b.MinQ + b.MaxQ) / 2, (b.MinR + b.MaxR) / 2);
        return map.Hexes.MinBy(h => h.DistanceTo(target));
    }
}
