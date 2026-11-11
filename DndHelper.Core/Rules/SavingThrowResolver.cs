namespace DndHelper.Core.Rules;

public sealed class SavingThrowResolver(DiceRoller roller)
{
    private static readonly Condition[] AutoFailStrengthDexterity =
        [Condition.Paralyzed, Condition.Stunned, Condition.Unconscious, Condition.Petrified];

    /// <summary>Rolls a saving throw. A natural 20 or 1 has no special effect on saves.</summary>
    public SaveResult Resolve(
        Combatant target,
        Ability ability,
        int dc,
        RollMode extraMode = RollMode.Normal,
        int extraBonus = 0,
        CoverType cover = CoverType.None)
    {
        var conditions = target.ActiveConditions;
        var reasons = new List<string>();

        if (ability is Ability.Strength or Ability.Dexterity && conditions.Overlaps(AutoFailStrengthDexterity))
        {
            var cause = AutoFailStrengthDexterity.First(conditions.Contains);
            reasons.Add($"{target.Name} is {cause}");
            return new SaveResult(ability, dc, false, true, null, RollMode.Normal, reasons);
        }

        var advantage = extraMode == RollMode.Advantage;
        var disadvantage = extraMode == RollMode.Disadvantage;

        if (target.SaveAdvantages.Contains(ability))
        {
            advantage = true;
            reasons.Add("advantage on this save");
        }

        if (target.SaveDisadvantages.Contains(ability))
        {
            disadvantage = true;
            reasons.Add("disadvantage on this save");
        }

        if (ability == Ability.Dexterity && conditions.Contains(Condition.Restrained))
        {
            disadvantage = true;
            reasons.Add("Restrained: disadvantage on Dexterity saves");
        }

        var mode = RollModeExtensions.Combine(advantage, disadvantage);

        var bonus = target.SaveModifier(ability)
                    + target.SaveBonusFromEffects
                    + extraBonus
                    - target.D20Penalty
                    + (ability == Ability.Dexterity ? cover.DexteritySaveBonus() : 0);

        var roll = roller.RollD20(mode, bonus);
        return new SaveResult(ability, dc, roll.Total >= dc, false, roll, mode, reasons);
    }
}