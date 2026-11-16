namespace DndHelper.Core.Rules;

/// <summary>A copy of a combatant's changeable state, used for preview and undo.</summary>
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
