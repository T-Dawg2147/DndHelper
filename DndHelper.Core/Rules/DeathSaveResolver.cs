namespace DndHelper.Core.Rules;

public enum DeathSaveOutcome
{
    Success,
    Failure,
    CriticalSuccess,
    CriticalFailure,
    Stabilized,
    Died,
    NotDying
}

public sealed record DeathSaveResult(
    Combatant Target,
    CombatantSnapshot Before,
    DeathSaveOutcome Outcome,
    D20Result? Roll,
    string Message)
{
    public void Undo() => Target.Restore(Before);
}

public sealed class DeathSaveResolver(DiceRoller roller)
{
    /// <summary>
    /// Rolls a death saving throw: 10+ succeeds, 9 or less fails, a natural 1 counts as two failures
    /// and a natural 20 restores 1 HP. Three successes stabilize; three failures kill.
    /// </summary>
    public DeathSaveResult Roll(Combatant target, RollMode mode = RollMode.Normal, int bonus = 0)
    {
        var before = target.Capture();

        if (!target.IsDying)
            return new DeathSaveResult(target, before, DeathSaveOutcome.NotDying, null, $"{target.Name} is not dying.");

        var roll = roller.RollD20(mode, bonus);

        if (roll.IsNatural20)
        {
            target.CurrentHp = 1;
            RecoveryResolver.Wake(target);
            return new DeathSaveResult(target, before, DeathSaveOutcome.CriticalSuccess, roll,
                $"{roll}: natural 20! {target.Name} regains 1 HP and wakes up.");
        }

        if (roll.IsNatural1)
            target.DeathSaveFailures = Math.Min(3, target.DeathSaveFailures + 2);
        else if (roll.Total >= 10)
            target.DeathSaveSuccesses++;
        else
            target.DeathSaveFailures++;

        var outcome = roll.IsNatural1 ? DeathSaveOutcome.CriticalFailure
            : roll.Total >= 10 ? DeathSaveOutcome.Success
            : DeathSaveOutcome.Failure;

        if (target.DeathSaveFailures >= 3)
        {
            target.IsDead = true;
            return new DeathSaveResult(target, before, DeathSaveOutcome.Died, roll, $"{roll}: {target.Name} dies.");
        }

        if (target.DeathSaveSuccesses >= 3)
        {
            target.IsStable = true;
            target.DeathSaveSuccesses = 0;
            target.DeathSaveFailures = 0;
            return new DeathSaveResult(target, before, DeathSaveOutcome.Stabilized, roll,
                $"{roll}: {target.Name} is stable at 0 HP.");
        }

        return new DeathSaveResult(target, before, outcome, roll,
            $"{roll}: {outcome} ({target.DeathSaveSuccesses} successes, {target.DeathSaveFailures} failures).");
    }
}