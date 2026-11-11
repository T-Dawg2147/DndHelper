namespace DndHelper.Core.Rules;

public enum TurnTiming
{
    StartOfTurn,
    EndOfTurn
}

public sealed record DamageOverTime(DiceExpression Damage, DamageType Type, TurnTiming Timing, bool IsMagical = true);

/// <summary>A save the affected creature repeats each turn; success ends the effect.</summary>
public sealed record RepeatSave(Ability Ability, int Dc, TurnTiming Timing);

/// <summary>A temporary effect on a creature: conditions, stat bonuses, granted resistances, damage over time.</summary>
public sealed class ActiveEffect
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    /// <summary>Id of the combatant that created the effect (the caster), used for concentration.</summary>
    public string? SourceId { get; set; }

    /// <summary>Rounds left; null means it lasts until removed. Ticks down at the end of the affected creature's turn.</summary>
    public int? RemainingRounds { get; set; }

    public bool RequiresConcentration { get; set; }

    public HashSet<Condition> GrantedConditions { get; private set; } = [];
    public HashSet<DamageType> GrantedResistances { get; private set; } = [];

    public int ArmorClassBonus { get; set; }
    public int SpeedBonus { get; set; }
    public int AttackBonus { get; set; }
    public int SaveBonus { get; set; }

    public DamageOverTime? DamageOverTime { get; set; }
    public RepeatSave? RepeatSave { get; set; }

    /// <summary>Exact copy including the Id. Used for undo snapshots.</summary>
    public ActiveEffect Clone()
    {
        var copy = (ActiveEffect)MemberwiseClone();
        copy.GrantedConditions = [.. GrantedConditions];
        copy.GrantedResistances = [.. GrantedResistances];
        return copy;
    }

    /// <summary>A fresh copy with a new Id, for applying one template to several targets.</summary>
    public ActiveEffect Instantiate(string? sourceId = null)
    {
        var copy = Clone();
        copy.Id = Guid.NewGuid();
        if (sourceId is not null) copy.SourceId = sourceId;
        return copy;
    }
}