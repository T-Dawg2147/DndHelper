using DndHelper.Core.Models;
using DndHelper.Core.Rules;

namespace DndHelper.Core.Characters;

/// <summary>Every derived number for a character, ready to display or to build a combatant from.</summary>
public sealed class CharacterSheet
{
    public required AbilityScores Scores { get; init; }
    public required int ProficiencyBonus { get; init; }
    public required int MaxHp { get; init; }
    public required int ArmorClass { get; init; }
    public required int Speed { get; init; }
    public required int Initiative { get; init; }
    public required int PassivePerception { get; init; }
    public required int DarkvisionFeet { get; init; }
    public required string Size { get; init; }

    public required IReadOnlySet<Ability> SaveProficiencies { get; init; }
    public required IReadOnlyDictionary<Skill, int> SkillBonuses { get; init; }
    public required IReadOnlySet<Skill> SkillProficiencies { get; init; }
    public required IReadOnlyList<DamageType> Resistances { get; init; }

    public int? SpellSaveDc { get; init; }
    public int? SpellAttackBonus { get; init; }
    public required int[] SpellSlots { get; init; }
    public required CasterType Caster { get; init; }

    public required string OriginFeat { get; init; }
    public required string ToolProficiency { get; init; }

    public int SaveBonus(Ability ability)
        => Scores.Modifier(ability) + (SaveProficiencies.Contains(ability) ? ProficiencyBonus : 0);

    public IReadOnlyList<LabelValue> Properties
    {
        get
        {
            var list = new List<LabelValue>
            {
                new("Hit Points", MaxHp.ToString()),
                new("Armor Class", ArmorClass.ToString()),
                new("Speed", $"{Speed} ft."),
                new("Size", Size),
                new("Initiative", Creature.Signed(Initiative)),
                new("Proficiency Bonus", Creature.Signed(ProficiencyBonus)),
                new("Abilities", string.Join("   ", AbilityExtensions.All.Select(a =>
                    $"{a.Abbreviation()} {Scores[a]} ({Creature.Signed(Scores.Modifier(a))})"))),
                new("Saving Throws", string.Join(", ", AbilityExtensions.All.Select(a =>
                    $"{a.Abbreviation()} {Creature.Signed(SaveBonus(a))}{(SaveProficiencies.Contains(a) ? "*" : "")}"))),
                new("Skills", string.Join(", ", SkillProficiencies.OrderBy(s => s.DisplayName())
                    .Select(s => $"{s.DisplayName()} {Creature.Signed(SkillBonuses[s])}"))),
                new("Passive Perception", PassivePerception.ToString())
            };

            if (DarkvisionFeet > 0)
                list.Add(new LabelValue("Darkvision", $"{DarkvisionFeet} ft."));

            if (Resistances.Count > 0)
                list.Add(new LabelValue("Resistances", string.Join(", ", Resistances)));

            if (SpellSaveDc is { } dc && SpellAttackBonus is { } attack)
                list.Add(new LabelValue("Spellcasting", $"Save DC {dc}, attack {Creature.Signed(attack)}"));

            if (SpellSlots.Any(s => s > 0))
            {
                list.Add(new LabelValue("Spell Slots", string.Join(", ", SpellSlots
                    .Select((count, i) => (count, level: i + 1))
                    .Where(s => s.count > 0)
                    .Select(s => $"L{s.level}: {s.count}"))));
            }
            else if (Caster == CasterType.Pact)
            {
                list.Add(new LabelValue("Spell Slots", "Pact Magic slots are not calculated yet."));
            }

            list.Add(new LabelValue("Origin Feat", OriginFeat));
            list.Add(new LabelValue("Tool Proficiency", ToolProficiency));
            return list;
        }
    }
}