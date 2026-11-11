namespace DndHelper.Core.Rules;

/// <summary>A creature taking part in rules calculations (player character, NPC or monster) with its live combat state.</summary>
public sealed class Combatant
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";

    /// <summary>Player characters make death saving throws; monsters and NPCs die at 0 HP.</summary>
    public bool IsPlayerCharacter { get; set; }

    public AbilityScores Abilities { get; set; } = new();
    public int ProficiencyBonus { get; set; } = 2;
    public HashSet<Ability> SaveProficiencies { get; } = [];
    public HashSet<Ability> SaveAdvantages { get; } = [];
    public HashSet<Ability> SaveDisadvantages { get; } = [];

    public int MaxHp { get; set; }
    public int CurrentHp { get; set; }
    public int TempHp { get; set; }

    public int BaseArmorClass { get; set; } = 10;
    public int BaseSpeed { get; set; } = 30;

    public DamageTraits Resistances { get; set; } = new();
    public DamageTraits Immunities { get; set; } = new();
    public DamageTraits Vulnerabilities { get; set; } = new();
    public HashSet<Condition> ConditionImmunities { get; } = [];

    /// <summary>Conditions applied directly (not through an effect).</summary>
    public HashSet<Condition> Conditions { get; } = [];

    /// <summary>Exhaustion level 0-6 (2024 rules: -2 per level to D20 Tests, -5 ft. Speed per level).</summary>
    public int ExhaustionLevel { get; set; }

    public List<ActiveEffect> Effects { get; } = [];

    public int DeathSaveSuccesses { get; set; }
    public int DeathSaveFailures { get; set; }
    public bool IsStable { get; set; }
    public bool IsDead { get; set; }

    /// <summary>Takes no damage on a successful Dexterity save and half on a failed one.</summary>
    public bool HasEvasion { get; set; }

    /// <summary>Advantage on Constitution saves to maintain concentration (e.g. War Caster).</summary>
    public bool ConcentrationAdvantage { get; set; }

    public string? ConcentratingOn { get; set; }

    public bool IsDying => IsPlayerCharacter && CurrentHp == 0 && !IsStable && !IsDead;

    /// <summary>All conditions currently affecting the creature, including implied ones, minus immunities.</summary>
    public HashSet<Condition> ActiveConditions
    {
        get
        {
            var raw = new HashSet<Condition>(Conditions);
            foreach (var effect in Effects)
                raw.UnionWith(effect.GrantedConditions);

            raw.ExceptWith(ConditionImmunities);
            return ConditionRules.WithImplied(raw);
        }
    }

    public bool HasCondition(Condition condition) => ActiveConditions.Contains(condition);

    public bool IsIncapacitated => HasCondition(Condition.Incapacitated);

    public int ArmorClass => BaseArmorClass + Effects.Sum(e => e.ArmorClassBonus);

    public int Speed
    {
        get
        {
            if (ActiveConditions.Overlaps(ConditionRules.ZeroSpeed)) return 0;
            return Math.Max(0, BaseSpeed + Effects.Sum(e => e.SpeedBonus) - 5 * ExhaustionLevel);
        }
    }

    /// <summary>Penalty subtracted from every D20 Test (attack rolls, checks, saves) due to exhaustion.</summary>
    public int D20Penalty => 2 * ExhaustionLevel;

    public int AttackBonusFromEffects => Effects.Sum(e => e.AttackBonus);
    public int SaveBonusFromEffects => Effects.Sum(e => e.SaveBonus);

    public bool HasResistance(DamageType type, bool isMagical)
        => Resistances.Applies(type, isMagical) || Effects.Any(e => e.GrantedResistances.Contains(type));

    public int SaveModifier(Ability ability)
        => Abilities.Modifier(ability) + (SaveProficiencies.Contains(ability) ? ProficiencyBonus : 0);

    public CombatantSnapshot Capture() => new(
        CurrentHp,
        TempHp,
        [.. Conditions],
        ExhaustionLevel,
        Effects.Select(e => e.Clone()).ToList(),
        DeathSaveSuccesses,
        DeathSaveFailures,
        IsStable,
        IsDead,
        ConcentratingOn);

    public void Restore(CombatantSnapshot snapshot)
    {
        CurrentHp = snapshot.CurrentHp;
        TempHp = snapshot.TempHp;

        Conditions.Clear();
        Conditions.UnionWith(snapshot.Conditions);

        ExhaustionLevel = snapshot.ExhaustionLevel;

        Effects.Clear();
        Effects.AddRange(snapshot.Effects.Select(e => e.Clone()));

        DeathSaveSuccesses = snapshot.DeathSaveSuccesses;
        DeathSaveFailures = snapshot.DeathSaveFailures;
        IsStable = snapshot.IsStable;
        IsDead = snapshot.IsDead;
        ConcentratingOn = snapshot.ConcentratingOn;
    }
}