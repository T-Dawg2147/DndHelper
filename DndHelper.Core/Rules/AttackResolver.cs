namespace DndHelper.Core.Rules;

public sealed class AttackResolver(DiceRoller roller)
{
    /// <summary>
    /// Rolls an attack and, on a hit, its damage. A natural 20 always hits and is a critical hit; a natural 1 always misses.
    /// The returned damage is not applied; pass it to <see cref="DamageResolver"/>.
    /// </summary>
    public AttackResult Resolve(AttackRequest request)
    {
        if (!request.Cover.CanBeTargeted())
        {
            return new AttackResult(null, 0, false, false, RollMode.Normal,
                [$"{request.Target.Name} has total cover and cannot be targeted."], null, null);
        }

        var advantage = AdvantageRules.ForAttack(
            request.Attacker, request.Target, request.TargetWithin5Feet,
            request.AttackerSeesFearSource, request.ExtraAdvantage, request.ExtraDisadvantage);

        var armorClass = request.Target.ArmorClass + request.Cover.ArmorClassBonus();
        var bonus = request.ToHitBonus + request.Attacker.AttackBonusFromEffects - request.Attacker.D20Penalty;
        var roll = roller.RollD20(advantage.Mode, bonus);

        var hit = roll.IsNatural20 || (!roll.IsNatural1 && roll.Total >= armorClass);
        var critical = hit && (roll.IsNatural20 || AdvantageRules.IsAutomaticCritical(request.Target, request.TargetWithin5Feet));

        var reasons = new List<string>(advantage.Reasons);
        if (request.Cover != CoverType.None)
            reasons.Add($"{request.Cover} cover: +{request.Cover.ArmorClassBonus()} AC");

        DiceRollResult? damageRoll = null;
        DamageInstance? damage = null;

        if (hit && request.Damage is not null)
        {
            damageRoll = roller.Roll(request.Damage, critical);
            damage = new DamageInstance(Math.Max(0, damageRoll.Total), request.DamageType, request.IsMagical, critical);
        }

        return new AttackResult(roll, armorClass, hit, critical, advantage.Mode, reasons, damageRoll, damage);
    }
}