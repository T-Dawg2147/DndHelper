using DndHelper.Core.Rules;

namespace DndHelper.Core.Characters;

public sealed record SpeciesDefinition(
    string Name,
    string Size,
    int Speed,
    int DarkvisionFeet,
    IReadOnlyList<DamageType> Resistances);

/// <summary>
/// The nine 2024 SRD species. Choices that depend on a lineage or ancestry (Dragonborn, Goliath, Tiefling,
/// Elf and Gnome lineages) are not modelled yet, so their resistances are left out.
/// </summary>
public static class SpeciesCatalog
{
    public static readonly IReadOnlyList<SpeciesDefinition> All =
    [
        new("Dragonborn", "Medium", 30, 60, []),
        new("Dwarf", "Medium", 30, 120, [DamageType.Poison]),
        new("Elf", "Medium", 30, 60, []),
        new("Gnome", "Small", 30, 60, []),
        new("Goliath", "Medium", 35, 0, []),
        new("Halfling", "Small", 30, 0, []),
        new("Human", "Medium", 30, 0, []),
        new("Orc", "Medium", 30, 120, []),
        new("Tiefling", "Medium", 30, 60, [])
    ];

    public static IReadOnlyList<string> Names => All.Select(s => s.Name).ToList();

    public static SpeciesDefinition? Find(string? name)
        => All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
}