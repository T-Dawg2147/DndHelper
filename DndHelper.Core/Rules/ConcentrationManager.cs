namespace DndHelper.Core.Rules;

public sealed record ConcentrationCheckResult(
    bool WasConcentrating,
    bool Maintained,
    int RequiredDc,
    SaveResult? Save,
    string Message);

/// <summary>Tracks who is concentrating on what, and ends the matching effects on every creature when concentration drops.</summary>
public sealed class ConcentrationManager(SavingThrowResolver saves)
{
    /// <summary>DC is 10 or half the damage taken, whichever is higher, to a maximum of 30.</summary>
    public static int RequiredDc(int damageTaken) => Math.Min(30, Math.Max(10, damageTaken / 2));

    /// <summary>Starts concentrating, ending any previous concentration first (only one at a time).</summary>
    public void Begin(Combatant caster, string spellName, IEnumerable<Combatant> everyone)
    {
        if (caster.ConcentratingOn is not null)
            End(caster, everyone);

        caster.ConcentratingOn = spellName;
    }

    /// <summary>Ends concentration and removes every concentration effect this caster created. Returns how many were removed.</summary>
    public int End(Combatant caster, IEnumerable<Combatant> everyone)
    {
        var removed = 0;
        foreach (var combatant in everyone)
            removed += combatant.Effects.RemoveAll(e => e.RequiresConcentration && e.SourceId == caster.Id);

        caster.ConcentratingOn = null;
        return removed;
    }

    /// <summary>
    /// Call after a concentrating creature takes damage. Incapacitated or dead creatures lose concentration automatically;
    /// otherwise a Constitution save is rolled.
    /// </summary>
    public ConcentrationCheckResult CheckAfterDamage(Combatant caster, int damageTaken, IEnumerable<Combatant> everyone)
    {
        if (caster.ConcentratingOn is null)
            return new ConcentrationCheckResult(false, true, 0, null, $"{caster.Name} is not concentrating.");

        var spell = caster.ConcentratingOn;

        if (caster.IsDead || caster.IsIncapacitated || caster.CurrentHp == 0)
        {
            End(caster, everyone);
            return new ConcentrationCheckResult(true, false, 0, null,
                $"{caster.Name} can no longer concentrate on {spell} (incapacitated or down).");
        }

        if (damageTaken <= 0)
            return new ConcentrationCheckResult(true, true, 0, null, $"{caster.Name} took no damage and keeps concentrating on {spell}.");

        var dc = RequiredDc(damageTaken);
        var save = saves.Resolve(caster, Ability.Constitution, dc,
            caster.ConcentrationAdvantage ? RollMode.Advantage : RollMode.Normal);

        if (save.Success)
            return new ConcentrationCheckResult(true, true, dc, save, $"{caster.Name} keeps concentrating on {spell}. {save}");

        End(caster, everyone);
        return new ConcentrationCheckResult(true, false, dc, save, $"{caster.Name} loses concentration on {spell}. {save}");
    }
}