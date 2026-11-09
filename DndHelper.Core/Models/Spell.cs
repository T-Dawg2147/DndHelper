using System.Text.Json.Serialization;

namespace DndHelper.Core.Models;

/// <summary> Source-agnostic spell definition, including the area-of-effect data needed by later milestones </summary>
public sealed class Spell
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; }
    public string School { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string HigherLevel { get; init; } = string.Empty;

    public string CastingTime { get; init; } = string.Empty;
    public string Range { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;

    public bool Ritual { get; init; }
    public bool Concentration { get; init; }
    public bool Verbal { get; init; }
    public bool Somatic { get; init; }
    public bool Material { get; init; }
    public string MaterialDescription { get; init; } = string.Empty;

    public string SavingThrowAbility { get; init; } = string.Empty;

    public bool AttackRoll { get; init; }

    public string DamageRoll { get; init; } = string.Empty;
    public List<string> DamageTypes { get; init; } = [];

    public string? ShapeType { get; init; }

    public double? ShapeSize { get; init; }
    public string? ShapeSizeUnit { get; init; }
    public string? TargetType { get; init; }

    public List<string> Classes { get; init; } = [];

    public string SourceKey { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;

    [JsonIgnore]
    public string Subtitle
    {
        get
        {
            var text = Level == 0 ? $"{School} cantrip" : $"Level {Level} {School}";
            if (Ritual) text += " (ritual)";
            return text.Trim();
        }
    }

    [JsonIgnore]
    public bool HasHigherLevel => !string.IsNullOrWhiteSpace(HigherLevel);

    [JsonIgnore]
    public string ComponentsText
    {
        get
        {
            var parts = new List<string>();
            if (Verbal) parts.Add("V");
            if (Somatic) parts.Add("S");
            if (Material) parts.Add(string.IsNullOrWhiteSpace(MaterialDescription) ? "M" : $"M ({MaterialDescription})");
            return string.Join(", ", parts);
        }
    }

    [JsonIgnore]
    public string DurationText => Concentration ? $"Concentration, {Duration}" : Duration;
    
    [JsonIgnore]
    public string AreaText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ShapeType)) return "";
            var size = ShapeSize is { } s ? $"{s:0.##} {ShapeSizeUnit ?? "ft."} " : "";
            return $"{size}{ShapeType}".Trim();
        }
    }

    [JsonIgnore]
    public IReadOnlyList<LabelValue> Properties
    {
        get
        {
            var list = new List<LabelValue>();
            Add(list, "Casting Time", CastingTime);
            Add(list, "Range", Range);
            Add(list, "Components", ComponentsText);
            Add(list, "Duration", DurationText);
            Add(list, "Area", AreaText);
            Add(list, "Saving Throw", Capitalize(SavingThrowAbility));
            Add(list, "Attack Roll", AttackRoll ? "Yes" : "");
            Add(list, "Damage", string.IsNullOrWhiteSpace(DamageRoll) ? "" : $"{DamageRoll} {string.Join(", ", DamageTypes)}".Trim());
            Add(list, "Classes", string.Join(", ", Classes));
            return list;
        }
    }

    private static void Add(List<LabelValue> list, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            list.Add(new LabelValue(label, value));
    }

    private static string Capitalize(string value)
        => string.IsNullOrEmpty(value) ? "" : char.ToUpperInvariant(value[0]) + value[1..];
}