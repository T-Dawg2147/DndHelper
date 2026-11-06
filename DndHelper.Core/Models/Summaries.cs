namespace DndHelper.Core.Models;

public sealed record CreatureSummary(
    string Key,
    string Name,
    string Type,
    string Size,
    double ChallengeRating,
    string ChallengeRatingText,
    string SourceName)
{
    public string Subtitle => $"CR {ChallengeRatingText} · {Size} {Type}".Trim();
}

public sealed record SpellSummary(
    string Key,
    string Name,
    int Level,
    string School,
    bool Concentration,
    bool Ritual,
    string SourceName)
{
    public string Subtitle
    {
        get
        {
            var text = Level == 0 ? $"Cantrip · {School}" : $"Level {Level} · {School}";
            if (Concentration) text += " · C";
            if (Ritual) text += " · R";
            return text;
        }
    }
}

public sealed record CreatureQuery(string? Text = null, string? Type = null, double? MinChallengeRating = null, double? MaxChallengeRating = null);
public sealed record SpellQuery(string? Text = null, int? Level = null, string? School = null, string? ClassName = null);