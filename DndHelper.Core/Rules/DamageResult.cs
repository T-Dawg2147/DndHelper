namespace DndHelper.Core.Rules;

/// <param name="IsMagical">False for ordinary weapon attacks; matters for "nonmagical" resistances.</param>
/// <param name="IsCritical">A critical hit; matters for death saving throw failures at 0 HP.</param>
public sealed record DamageInstance(int Amount, DamageType Type, bool IsMagical = true, bool IsCritical = false);

/// <summary>Everything that happened when damage was applied, with a step-by-step audit trail and a snapshot for undo.</summary>
public sealed record DamageResult(
    Combatant Target,
    CombatantSnapshot Before,
    int RawTotal,
    int AdjustedTotal,
    int AbsorbedByTempHp,
    int HpLost,
    bool DroppedToZero,
    bool InstantDeath,
    bool Died,
    int DeathSaveFailuresAdded,
    IReadOnlyList<string> Steps)
{
    /// <summary>Restores the target to how it was before this damage.</summary>
    public void Undo() => Target.Restore(Before);
}
