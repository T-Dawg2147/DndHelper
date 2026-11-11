namespace DndHelper.Core.Rules;

public sealed class AttackRequest
{
    public required Combatant Attacker { get; init; }
    public required Combatant Target { get; init; }

    public int ToHitBonus { get; init; }

    public DiceExpression? Damage { get; init; }
    public DamageType DamageType { get; init; } = DamageType.Slashing;

    public bool IsRanged { get; init; }
    public bool TargetWithin5Feet { get; init; } = true;
    public CoverType Cover { get; init; } = CoverType.None;
    public bool IsMagical { get; init; }
    public bool AttackerSeesFearSource { get; init; } = true;
    public bool ExtraAdvantage { get; init; }
    public bool ExtraDisadvantage { get; init; }
}

public sealed record AttackResult(
    D20Result? Roll,
    int TargetArmorClass,
    bool Hit, 
    bool Critical,
    RollMode Mode,
    IReadOnlyList<string> Reasons,
    DiceRollResult? DamageRoll,
    DamageInstance? Damage);