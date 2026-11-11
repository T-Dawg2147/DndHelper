using DndHelper.Core.Rules;

namespace DndHelper.Core.Characters;

public sealed record BackgroundDefinition(
    string Name,
    IReadOnlyList<Ability> AbilityOptions,
    IReadOnlyList<Skill> Skills,
    string OriginFeat,
    string ToolProficiency);

public static class BackgroundCatalog
{
    public static readonly IReadOnlyList<BackgroundDefinition> All =
    [
        new("Acolyte", [Ability.Intelligence, Ability.Wisdom, Ability.Charisma],
            [Skill.Insight, Skill.Religion], "Magic Initiate (Cleric)", "Calligrapher's Supplies"),

        new("Criminal", [Ability.Dexterity, Ability.Constitution, Ability.Intelligence],
            [Skill.SleightOfHand, Skill.Stealth], "Alert", "Thieves' Tools"),

        new("Sage", [Ability.Constitution, Ability.Intelligence, Ability.Wisdom],
            [Skill.Arcana, Skill.History], "Magic Initiate (Wizard)", "Calligrapher's Supplies"),

        new("Soldier", [Ability.Strength, Ability.Dexterity, Ability.Constitution],
            [Skill.Athletics, Skill.Intimidation], "Savage Attacker", "One gaming set")
    ];

    public static IReadOnlyList<string> Names => All.Select(b => b.Name).ToList();

    public static BackgroundDefinition? Find(string? name)
        => All.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
}