namespace DndHelper.Core.Rules;

/// <summary>What a spell or area effect does, independent of who it hits.</summary>
public sealed class SpellEffectRequest
{
    public string Name { get; init; } = "Spell";
    public Combatant? Caster { get; init; }

    public DiceExpression? Damage { get; init; }
    public DamageType DamageType { get; init; } = DamageType.Fire;
    public bool IsMagical { get; init; } = true;

    public Ability? SaveAbility { get; init; }
    public int SaveDc { get; init; }

    /// <summary>True for "half as much damage on a successful save"; false for "no damage on a successful save".</summary>
    public bool HalfDamageOnSave { get; init; } = true;

    /// <summary>Applied to targets that fail the save (or to every target when there is no save).</summary>
    public ActiveEffect? Effect { get; init; }

    /// <summary>Cover for each target, keyed by combatant Id.</summary>
    public IReadOnlyDictionary<string, CoverType>? CoverByTargetId { get; init; }
}