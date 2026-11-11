using DndHelper.Core.Models;

namespace DndHelper.Core.Rules;

public static class CombatantFactory
{
    /// <summary>Creates a combatant from a stat block loaded from the content database.</summary>
    public static Combatant FromCreature(Creature creature, string? name = null)
    {
        var combatant = new Combatant
        {
            Name = name ?? creature.Name,
            IsPlayerCharacter = false,
            Abilities = AbilityScores.FromDictionary(creature.AbilityScores),
            ProficiencyBonus = creature.ProficiencyBonus ?? Proficiency.ForChallengeRating(creature.ChallengeRating),
            MaxHp = creature.HitPoints,
            CurrentHp = creature.HitPoints,
            BaseArmorClass = creature.ArmorClass,
            BaseSpeed = creature.WalkSpeed ?? 30,
            Resistances = DamageTraits.Parse(creature.DamageResistances),
            Immunities = DamageTraits.Parse(creature.DamageImmunities),
            Vulnerabilities = DamageTraits.Parse(creature.DamageVulnerabilities)
        };

        foreach (var text in creature.SavingThrowProficiencies)
        {
            if (AbilityExtensions.TryParse(text, out var ability))
                combatant.SaveProficiencies.Add(ability);
        }

        var immunityText = string.Join(", ", creature.ConditionImmunities);
        foreach (var condition in Enum.GetValues<Condition>())
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(immunityText, $@"\b{condition}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                combatant.ConditionImmunities.Add(condition);
        }

        return combatant;
    }
}