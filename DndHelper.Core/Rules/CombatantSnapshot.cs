namespace DndHelper.Core.Rules;

public sealed record CombatantSnapshot(
    int CurrentHp,
    int TempHp,
    IReadOnlyList<Condition> Conditions,
    int ExhaustionLevel,
    IReadOnlyList<ActiveEffect> Effects,
    int DeathSaveSuccesses,
    int DeathSaveFailures,
    bool IsStable,
    bool IsDead,
    string? ConcentratingOn);