using System.Text.Json;
using System.Text.Json.Serialization;
using DndHelper.Maps.Hexes;
using DndHelper.Maps.Maps;
using DndHelper.Maps.Tokens;

namespace DndHelper.Maps.Persistence;

public sealed record MapDocument(HexMap Map, IReadOnlyList<MapToken> Tokens);

/// <summary>Reads and writes maps as JSON. Loading validates everything, since map files can come from anywhere.</summary>
public static class MapSerializer
{
    public const int CurrentVersion = 1;
    public const int MaxCells = 100_000;
    public const int MaxTokens = 1_000;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class CellDto
    {
        public int Q { get; set; }
        public int R { get; set; }
        public Terrain Terrain { get; set; }
    }

    private sealed class FileDto
    {
        public int Version { get; set; }
        public string Name { get; set; } = "";
        public int FeetPerHex { get; set; } = 5;
        public List<CellDto> Cells { get; set; } = [];
        public List<MapToken> Tokens { get; set; } = [];
    }

    public static string Serialize(HexMap map, IEnumerable<MapToken> tokens)
    {
        var dto = new FileDto
        {
            Version = CurrentVersion,
            Name = map.Name,
            FeetPerHex = map.FeetPerHex,
            Cells = map.Cells
                .OrderBy(c => c.Key.R).ThenBy(c => c.Key.Q)
                .Select(c => new CellDto { Q = c.Key.Q, R = c.Key.R, Terrain = c.Value })
                .ToList(),
            Tokens = tokens.ToList()
        };

        return JsonSerializer.Serialize(dto, Options);
    }

    /// <exception cref="FormatException">The text is not a valid, supported map file.</exception>
    public static MapDocument Deserialize(string json)
    {
        FileDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<FileDto>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new FormatException("This is not a valid map file.", ex);
        }

        if (dto is null) throw new FormatException("This map file is empty.");
        if (dto.Version < 1 || dto.Version > CurrentVersion)
            throw new FormatException($"Map file version {dto.Version} is not supported (this app reads up to {CurrentVersion}).");
        if (dto.Cells.Count > MaxCells) throw new FormatException($"The map has too many hexes (limit {MaxCells:N0}).");
        if (dto.Tokens.Count > MaxTokens) throw new FormatException($"The map has too many tokens (limit {MaxTokens:N0}).");

        var map = new HexMap
        {
            Name = string.IsNullOrWhiteSpace(dto.Name) ? "Untitled map" : dto.Name.Trim(),
            FeetPerHex = Math.Clamp(dto.FeetPerHex, 1, 100)
        };

        foreach (var cell in dto.Cells)
        {
            if (!Enum.IsDefined(cell.Terrain))
                throw new FormatException($"Unknown terrain at ({cell.Q}, {cell.R}).");

            map.Set(new Hex(cell.Q, cell.R), cell.Terrain);
        }

        var tokens = new List<MapToken>(dto.Tokens.Count);
        foreach (var token in dto.Tokens)
        {
            if (token is null) continue;

            tokens.Add(new MapToken
            {
                Id = string.IsNullOrWhiteSpace(token.Id) ? Guid.NewGuid().ToString("N") : token.Id,
                Name = string.IsNullOrWhiteSpace(token.Name) ? "Token" : token.Name.Trim(),
                Hex = token.Hex,
                Team = Enum.IsDefined(token.Team) ? token.Team : TokenTeam.Neutral,
                SpeedFeet = Math.Clamp(token.SpeedFeet, 0, 1000)
            });
        }

        return new MapDocument(map, tokens);
    }
}
