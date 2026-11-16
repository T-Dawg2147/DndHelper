using System.Text.Json.Serialization;
using DndHelper.Maps.Hexes;

namespace DndHelper.Maps.Tokens;

public enum TokenTeam
{
    Player,
    Enemy,
    Neutral
}

/// <summary>How a token relates to the area effect currently drawn on the map.</summary>
public enum TokenAreaState
{
    None,

    /// <summary>Inside the area and reached by the effect.</summary>
    Hit,

    /// <summary>Inside the area but shielded (for example by a wall).</summary>
    Blocked
}

public static class TokenColors
{
    public static string Hex(TokenTeam team) => team switch
    {
        TokenTeam.Player => "#3DA35D",
        TokenTeam.Enemy => "#D64545",
        _ => "#4C9AFF"
    };
}

/// <summary>A creature or character standing on the map.</summary>
public sealed class MapToken
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public Hex Hex { get; set; }
    public TokenTeam Team { get; set; } = TokenTeam.Neutral;

    /// <summary>Walking speed in feet, which limits how far the token can move.</summary>
    public int SpeedFeet { get; set; } = 30;

    // Everything below is live state shown on the map. It is never saved with the map file.

    /// <summary>Id of the encounter participant this token represents, if linked.</summary>
    [JsonIgnore]
    public string? ParticipantId { get; set; }

    /// <summary>Current HP divided by max HP (0 to 1), or null when the token is not linked to a combatant.</summary>
    [JsonIgnore]
    public double? HealthFraction { get; set; }

    /// <summary>True when the linked combatant is at 0 HP or dead.</summary>
    [JsonIgnore]
    public bool IsDown { get; set; }

    [JsonIgnore]
    public TokenAreaState AreaState { get; set; }

    /// <summary>Short label drawn on the token: initials of up to two words, e.g. "WR" for "Wolf Rider".</summary>
    [JsonIgnore]
    public string Initials
    {
        get
        {
            var words = Name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
            return words.Length switch
            {
                0 => "?",
                1 => words[0].Length == 1
                    ? words[0].ToUpperInvariant()
                    : char.ToUpperInvariant(words[0][0]) + words[0][1].ToString().ToLowerInvariant(),
                _ => string.Concat(char.ToUpperInvariant(words[0][0]), char.ToUpperInvariant(words[1][0]))
            };
        }
    }
}
