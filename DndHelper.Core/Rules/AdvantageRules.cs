namespace DndHelper.Core.Rules;

public sealed record AdvantageResult(RollMode Mode, IReadOnlyList<string> Reasons);

/// <summary>Works out advantage and disadvantage on an attack roll from conditions and circumstances.</summary>
public static class AdvantageRules
{
    public static AdvantageResult ForAttack(
        Combatant attacker,
        Combatant target,
        bool targetWithin5Feet,
        bool attackerSeesFearSource = true,
        bool extraAdvantage = false,
        bool extraDisadvantage = false)
    {
        var reasons = new List<string>();
        var advantage = extraAdvantage;
        var disadvantage = extraDisadvantage;

        if (extraAdvantage) reasons.Add("Advantage: other source");
        if (extraDisadvantage) reasons.Add("Disadvantage: other source");

        var attackerConditions = attacker.ActiveConditions;
        var targetConditions = target.ActiveConditions;

        void Adv(string reason) { advantage = true; reasons.Add("Advantage: " + reason); }
        void Dis(string reason) { disadvantage = true; reasons.Add("Disadvantage: " + reason); }

        if (attackerConditions.Contains(Condition.Blinded)) Dis("attacker is Blinded");
        if (attackerConditions.Contains(Condition.Poisoned)) Dis("attacker is Poisoned");
        if (attackerConditions.Contains(Condition.Prone)) Dis("attacker is Prone");
        if (attackerConditions.Contains(Condition.Restrained)) Dis("attacker is Restrained");
        if (attackerConditions.Contains(Condition.Frightened) && attackerSeesFearSource) Dis("attacker is Frightened");
        if (attackerConditions.Contains(Condition.Invisible)) Adv("attacker is Invisible");

        if (targetConditions.Contains(Condition.Blinded)) Adv("target is Blinded");
        if (targetConditions.Contains(Condition.Paralyzed)) Adv("target is Paralyzed");
        if (targetConditions.Contains(Condition.Petrified)) Adv("target is Petrified");
        if (targetConditions.Contains(Condition.Restrained)) Adv("target is Restrained");
        if (targetConditions.Contains(Condition.Stunned)) Adv("target is Stunned");
        if (targetConditions.Contains(Condition.Unconscious)) Adv("target is Unconscious");
        if (targetConditions.Contains(Condition.Invisible)) Dis("target is Invisible");

        if (targetConditions.Contains(Condition.Prone))
        {
            if (targetWithin5Feet) Adv("target is Prone and within 5 feet");
            else Dis("target is Prone and more than 5 feet away");
        }

        return new AdvantageResult(RollModeExtensions.Combine(advantage, disadvantage), reasons);
    }

    /// <summary>A hit against a Paralyzed or Unconscious target within 5 feet is automatically a critical hit.</summary>
    public static bool IsAutomaticCritical(Combatant target, bool targetWithin5Feet)
        => targetWithin5Feet
           && (target.HasCondition(Condition.Paralyzed) || target.HasCondition(Condition.Unconscious));
}