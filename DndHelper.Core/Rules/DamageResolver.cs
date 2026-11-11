namespace DndHelper.Core.Rules;

/// <summary>
/// Applies damage in the 2024 order: immunity, then resistance (halved, rounded down), then vulnerability (doubled),
/// then temporary hit points, then hit points. Dropping to 0 HP, massive damage and death save failures are handled here.
/// </summary>
public static class DamageResolver
{
    public static DamageResult Apply(Combatant target, IEnumerable<DamageInstance> instances)
    {
        var before = target.Capture();
        var steps = new List<string>();
        int raw = 0, adjustedTotal = 0, absorbed = 0, hpLost = 0, failuresAdded = 0;
        bool dropped = false, instantDeath = false;

        foreach (var hit in instances)
        {
            if (hit.Amount <= 0) continue;
            raw += hit.Amount;

            if (target.IsDead)
            {
                steps.Add($"{target.Name} is already dead; {hit.Amount} {hit.Type} damage ignored.");
                continue;
            }

            var amount = Adjust(target, hit, steps);
            adjustedTotal += amount;
            if (amount <= 0) continue;

            if (target.TempHp > 0)
            {
                var soaked = Math.Min(target.TempHp, amount);
                target.TempHp -= soaked;
                amount -= soaked;
                absorbed += soaked;
                steps.Add($"{soaked} absorbed by temporary hit points ({target.TempHp} left).");
            }

            if (amount <= 0) continue;

            if (target.CurrentHp > 0)
            {
                var lost = Math.Min(target.CurrentHp, amount);
                target.CurrentHp -= lost;
                amount -= lost;
                hpLost += lost;
                steps.Add($"{target.Name} loses {lost} HP ({target.CurrentHp}/{target.MaxHp}).");

                if (target.CurrentHp == 0)
                {
                    dropped = true;
                    DropToZero(target, amount, steps, ref instantDeath);
                }
            }
            else if (target.IsPlayerCharacter)
            {
                HitWhileAtZero(target, hit, amount, steps, ref instantDeath, ref failuresAdded);
            }
        }

        return new DamageResult(
            target, before, raw, adjustedTotal, absorbed, hpLost,
            dropped, instantDeath, !before.IsDead && target.IsDead, failuresAdded, steps);
    }

    /// <summary>Calculates the outcome without changing the target.</summary>
    public static DamageResult Preview(Combatant target, IEnumerable<DamageInstance> instances)
    {
        var result = Apply(target, instances);
        target.Restore(result.Before);
        return result;
    }

    private static int Adjust(Combatant target, DamageInstance hit, List<string> steps)
    {
        if (target.Immunities.Applies(hit.Type, hit.IsMagical))
        {
            steps.Add($"{target.Name} is immune to {hit.Type}: {hit.Amount} -> 0.");
            return 0;
        }

        var amount = hit.Amount;

        if (target.HasResistance(hit.Type, hit.IsMagical))
        {
            var halved = amount / 2;
            steps.Add($"Resistance to {hit.Type}: {amount} -> {halved}.");
            amount = halved;
        }

        if (target.Vulnerabilities.Applies(hit.Type, hit.IsMagical))
        {
            steps.Add($"Vulnerability to {hit.Type}: {amount} -> {amount * 2}.");
            amount *= 2;
        }

        if (amount == hit.Amount)
            steps.Add($"{amount} {hit.Type} damage.");

        return amount;
    }

    private static void DropToZero(Combatant target, int leftover, List<string> steps, ref bool instantDeath)
    {
        if (!target.IsPlayerCharacter)
        {
            target.IsDead = true;
            steps.Add($"{target.Name} is destroyed.");
            return;
        }

        if (leftover >= target.MaxHp)
        {
            target.IsDead = true;
            instantDeath = true;
            steps.Add($"{leftover} damage remained, at least {target.MaxHp} (max HP): instant death.");
            return;
        }

        target.Conditions.Add(Condition.Unconscious);
        target.IsStable = false;
        target.DeathSaveSuccesses = 0;
        target.DeathSaveFailures = 0;
        steps.Add($"{target.Name} drops to 0 HP and is unconscious and dying.");
    }

    private static void HitWhileAtZero(
        Combatant target, DamageInstance hit, int amount, List<string> steps, ref bool instantDeath, ref int failuresAdded)
    {
        if (amount >= target.MaxHp)
        {
            target.IsDead = true;
            instantDeath = true;
            steps.Add($"{amount} damage while at 0 HP, at least {target.MaxHp} (max HP): instant death.");
            return;
        }

        var failures = hit.IsCritical ? 2 : 1;
        target.IsStable = false;
        target.DeathSaveFailures = Math.Min(3, target.DeathSaveFailures + failures);
        failuresAdded += failures;
        steps.Add($"Damaged while at 0 HP: {failures} death save failure{(failures == 1 ? "" : "s")} ({target.DeathSaveFailures}/3).");

        if (target.DeathSaveFailures >= 3)
        {
            target.IsDead = true;
            steps.Add($"{target.Name} dies.");
        }
    }
}