namespace DndHelper.Core.Rules;

/// <summary>The planned outcome for one target. Nothing has been changed yet.</summary>
public sealed record TargetResolution(
    Combatant Target,
    CoverType Cover,
    SaveResult? Save,
    bool Skipped,
    int DamageAmount,
    bool EffectApplies,
    DamageResult? Preview,
    IReadOnlyList<string> Notes);

public sealed record CommittedResolution(
    IReadOnlyList<DamageResult> Damage,
    IReadOnlyList<ConcentrationCheckResult> ConcentrationChecks,
    IReadOnlyDictionary<Combatant, CombatantSnapshot> Snapshots)
{
    /// <summary>Puts every creature back exactly as it was before the spell was committed.</summary>
    public void Undo()
    {
        foreach (var (combatant, snapshot) in Snapshots)
            combatant.Restore(snapshot);
    }
}

public sealed class SpellResolutionPlan
{
    private readonly ConcentrationManager _concentration;
    private bool _committed;

    internal SpellResolutionPlan(
        SpellEffectRequest request,
        DiceRollResult? damageRoll,
        IReadOnlyList<TargetResolution> targets,
        ConcentrationManager concentration)
    {
        Request = request;
        DamageRoll = damageRoll;
        Targets = targets;
        _concentration = concentration;
    }

    public SpellEffectRequest Request { get; }

    /// <summary>The single damage roll shared by all targets.</summary>
    public DiceRollResult? DamageRoll { get; }

    public IReadOnlyList<TargetResolution> Targets { get; }

    /// <summary>
    /// Applies damage and effects, starts the caster's concentration if needed, and rolls concentration saves for damaged
    /// concentrating targets. <paramref name="everyone"/> should be every combatant in the encounter.
    /// </summary>
    public CommittedResolution Commit(IEnumerable<Combatant> everyone)
    {
        if (_committed)
            throw new InvalidOperationException("This spell resolution has already been committed.");
        _committed = true;

        var all = everyone.Concat(Targets.Select(t => t.Target));
        if (Request.Caster is not null) all = all.Append(Request.Caster);
        var participants = all.Distinct().ToList();

        var snapshots = participants.ToDictionary(c => c, c => c.Capture());
        var damageResults = new List<DamageResult>();
        var checks = new List<ConcentrationCheckResult>();

        if (Request.Effect is { RequiresConcentration: true } && Request.Caster is { } caster
            && Targets.Any(t => t.EffectApplies))
        {
            _concentration.Begin(caster, Request.Name, participants);
        }

        foreach (var planned in Targets.Where(t => !t.Skipped))
        {
            if (planned.DamageAmount > 0)
            {
                var result = DamageResolver.Apply(planned.Target,
                    [new DamageInstance(planned.DamageAmount, Request.DamageType, Request.IsMagical)]);
                damageResults.Add(result);

                if (planned.Target.ConcentratingOn is not null)
                    checks.Add(_concentration.CheckAfterDamage(planned.Target, result.AdjustedTotal, participants));
            }

            if (planned.EffectApplies && Request.Effect is { } template)
                planned.Target.Effects.Add(template.Instantiate(Request.Caster?.Id));
        }

        return new CommittedResolution(damageResults, checks, snapshots);
    }
}