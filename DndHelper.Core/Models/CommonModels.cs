namespace DndHelper.Core.Models;

public sealed record LabelValue(string Label, string Value);

public sealed class NamedEntry
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed record AbilityDisplays(string Abbreviation, int Score, string Modifier, string Save, bool SaveProficient);

public sealed class AttackEntry
{
    public string Name { get; init; } = string.Empty;
    public int? ToHit { get; init; }
    public int? DamageDieCount { get; init; }
    public string? DamageDieType { get; init; }
    public int? DamageBonus { get; init; }
    public string? DamageType { get; init; }
    public int? Reach { get; init; }
    public int? Range { get; init; }
    public int? LongRange { get; init; }
}

public sealed class ActionEntry
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public string ActionType { get; init; } = "ACTION";

    public int? LegendaryActionCost { get; init; }
    public List<AttackEntry> Attacks { get; init; } = [];
}

public sealed record ActionGroup(string Title, IReadOnlyList<ActionEntry> Entries);