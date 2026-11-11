using DndHelper.Core.Rules;

namespace DndHelper.Core.Characters;

public enum CasterType
{
    None,
    Full,
    Half,
    Pact
}

public sealed record ClassDefinition(
    string Name,
    int HitDie,
    IReadOnlyList<Ability> SavingThrows,
    int SkillChoiceCount,
    IReadOnlyList<Skill> SkillOptions,
    CasterType Caster,
    Ability? SpellcastingAbility);

/// <summary>The twelve 2024 SRD classes with the facts needed for calculations.</summary>
public static class ClassCatalog
{
    private static readonly IReadOnlyList<Skill> AnySkill = SkillExtensions.All;

    public static readonly IReadOnlyList<ClassDefinition> All =
    [
        new("Barbarian", 12, [Ability.Strength, Ability.Constitution], 2,
            [Skill.AnimalHandling, Skill.Athletics, Skill.Intimidation, Skill.Nature, Skill.Perception, Skill.Survival],
            CasterType.None, null),

        new("Bard", 8, [Ability.Dexterity, Ability.Charisma], 3, AnySkill, CasterType.Full, Ability.Charisma),

        new("Cleric", 8, [Ability.Wisdom, Ability.Charisma], 2,
            [Skill.History, Skill.Insight, Skill.Medicine, Skill.Persuasion, Skill.Religion],
            CasterType.Full, Ability.Wisdom),

        new("Druid", 8, [Ability.Intelligence, Ability.Wisdom], 2,
            [Skill.Arcana, Skill.AnimalHandling, Skill.Insight, Skill.Medicine, Skill.Nature, Skill.Perception, Skill.Religion, Skill.Survival],
            CasterType.Full, Ability.Wisdom),

        new("Fighter", 10, [Ability.Strength, Ability.Constitution], 2,
            [Skill.Acrobatics, Skill.AnimalHandling, Skill.Athletics, Skill.History, Skill.Insight, Skill.Intimidation, Skill.Persuasion, Skill.Perception, Skill.Survival],
            CasterType.None, null),

        new("Monk", 8, [Ability.Strength, Ability.Dexterity], 2,
            [Skill.Acrobatics, Skill.Athletics, Skill.History, Skill.Insight, Skill.Religion, Skill.Stealth],
            CasterType.None, null),

        new("Paladin", 10, [Ability.Wisdom, Ability.Charisma], 2,
            [Skill.Athletics, Skill.Insight, Skill.Intimidation, Skill.Medicine, Skill.Persuasion, Skill.Religion],
            CasterType.Half, Ability.Charisma),

        new("Ranger", 10, [Ability.Strength, Ability.Dexterity], 3,
            [Skill.AnimalHandling, Skill.Athletics, Skill.Insight, Skill.Investigation, Skill.Nature, Skill.Perception, Skill.Stealth, Skill.Survival],
            CasterType.Half, Ability.Wisdom),

        new("Rogue", 8, [Ability.Dexterity, Ability.Intelligence], 4,
            [Skill.Acrobatics, Skill.Athletics, Skill.Deception, Skill.Insight, Skill.Intimidation, Skill.Investigation, Skill.Perception, Skill.Persuasion, Skill.SleightOfHand, Skill.Stealth],
            CasterType.None, null),

        new("Sorcerer", 6, [Ability.Constitution, Ability.Charisma], 2,
            [Skill.Arcana, Skill.Deception, Skill.Insight, Skill.Intimidation, Skill.Persuasion, Skill.Religion],
            CasterType.Full, Ability.Charisma),

        new("Warlock", 8, [Ability.Wisdom, Ability.Charisma], 2,
            [Skill.Arcana, Skill.Deception, Skill.History, Skill.Intimidation, Skill.Investigation, Skill.Nature, Skill.Religion],
            CasterType.Pact, Ability.Charisma),

        new("Wizard", 6, [Ability.Intelligence, Ability.Wisdom], 2,
            [Skill.Arcana, Skill.History, Skill.Insight, Skill.Investigation, Skill.Medicine, Skill.Nature, Skill.Religion],
            CasterType.Full, Ability.Intelligence)
    ];

    public static IReadOnlyList<string> Names => All.Select(c => c.Name).ToList();

    public static ClassDefinition? Find(string? name)
        => All.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
}