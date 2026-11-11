namespace DndHelper.Core.Rules;

public sealed record RecoveryResult(Combatant Target, CombatantSnapshot Before, int HpRestored, bool Revived, string Message)
{
    public void Undo() => Target.Restore(Before);
}

public static class RecoveryResolver
{
    /// <summary>Restores hit points up to the maximum. Healing a player character at 0 HP wakes them and resets death saves.</summary>
    public static RecoveryResult Heal(Combatant target, int amount)
    {
        var before = target.Capture();

        if (target.IsDead)
            return new RecoveryResult(target, before, 0, false, $"{target.Name} is dead; healing has no effect.");

        if (amount <= 0)
            return new RecoveryResult(target, before, 0, false, "Nothing to heal.");

        var wasAtZero = target.CurrentHp == 0;
        var restored = Math.Min(amount, target.MaxHp - target.CurrentHp);
        target.CurrentHp += restored;

        var revived = false;
        if (wasAtZero && restored > 0 && target.IsPlayerCharacter)
        {
            Wake(target);
            revived = true;
        }

        return new RecoveryResult(target, before, restored, revived,
            $"{target.Name} regains {restored} HP ({target.CurrentHp}/{target.MaxHp}){(revived ? " and regains consciousness" : "")}.");
    }

    /// <summary>Temporary hit points do not stack: keep whichever amount is higher.</summary>
    public static RecoveryResult GrantTemporaryHp(Combatant target, int amount)
    {
        var before = target.Capture();
        var newValue = Math.Max(target.TempHp, Math.Max(0, amount));
        target.TempHp = newValue;

        return new RecoveryResult(target, before, 0, false,
            $"{target.Name} has {newValue} temporary HP.");
    }

    /// <summary>Stabilizes a dying creature (e.g. a successful Medicine check or Spare the Dying).</summary>
    public static RecoveryResult Stabilize(Combatant target)
    {
        var before = target.Capture();

        if (!target.IsDying)
            return new RecoveryResult(target, before, 0, false, $"{target.Name} is not dying.");

        target.IsStable = true;
        target.DeathSaveSuccesses = 0;
        target.DeathSaveFailures = 0;
        return new RecoveryResult(target, before, 0, false, $"{target.Name} is stable at 0 HP.");
    }

    internal static void Wake(Combatant target)
    {
        target.Conditions.Remove(Condition.Unconscious);
        target.DeathSaveSuccesses = 0;
        target.DeathSaveFailures = 0;
        target.IsStable = false;
    }
}