using DndHelper.Core.Rules;

namespace DndHelper.Core.Characters;

public static class CharacterCombatantFactory
{
    /// <summary>Builds a full-health combatant for the encounter tracker from a saved character.</summary>
    public static Combatant ToCombatant(PlayerCharacter character)
    {
        var sheet = CharacterCalculator.Compute(character);

        var combatant = new Combatant
        {
            Name = character.Name,
            IsPlayerCharacter = true,
            Abilities = sheet.Scores,
            ProficiencyBonus = sheet.ProficiencyBonus,
            MaxHp = sheet.MaxHp,
            CurrentHp = sheet.MaxHp,
            BaseArmorClass = sheet.ArmorClass,
            BaseSpeed = sheet.Speed
        };

        combatant.SaveProficiencies.UnionWith(sheet.SaveProficiencies);
        combatant.Resistances.Always.UnionWith(sheet.Resistances);
        return combatant;
    }
}