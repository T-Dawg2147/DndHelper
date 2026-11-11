namespace DndHelper.Core.Rules;

public sealed record DamageInstance(int Amount, DamageType Type, bool IsMagical = true, bool IsCritical = false);

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
    public void Undo() => Target.Restore(Before);
}