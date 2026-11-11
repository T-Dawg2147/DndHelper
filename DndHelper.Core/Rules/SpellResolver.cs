namespace DndHelper.Core.Rules;

public sealed class SpellResolver(DiceRoller roller, SavingThrowResolver saves, ConcentrationManager concentration)
{
    /// <summary>
    /// Rolls the damage once, rolls each target's save, and works out each target's outcome (including resistances) without
    /// changing anyone. Review the plan, then call <see cref="SpellResolutionPlan.Commit"/> to apply it.
    /// </summary>
    public SpellResolutionPlan Plan(SpellEffectRequest request, IEnumerable<Combatant> targets)
    {
        var damageRoll = request.Damage is null ? null : roller.Roll(request.Damage);
        var baseDamage = Math.Max(0, damageRoll?.Total ?? 0);
        var planned = new List<TargetResolution>();

        foreach (var target in targets)
        {
            var cover = CoverType.None;
            request.CoverByTargetId?.TryGetValue(target.Id, out cover);
            var notes = new List<string>();

            if (!cover.CanBeTargeted())
            {
                notes.Add("Total cover: not affected.");
                planned.Add(new TargetResolution(target, cover, null, true, 0, false, null, notes));
                continue;
            }

            SaveResult? save = null;
            var amount = baseDamage;
            var effectApplies = request.Effect is not null;

            if (request.SaveAbility is { } ability)
            {
                save = saves.Resolve(target, ability, request.SaveDc, cover: cover);
                effectApplies = request.Effect is not null && !save.Success;

                var evasion = target.HasEvasion && ability == Ability.Dexterity && !target.IsIncapacitated;

                if (save.Success)
                {
                    amount = evasion || !request.HalfDamageOnSave ? 0 : baseDamage / 2;
                    notes.Add(evasion ? "Evasion: no damage on a successful save." : "Saved.");
                }
                else if (evasion)
                {
                    amount = baseDamage / 2;
                    notes.Add("Evasion: half damage on a failed save.");
                }
            }

            var preview = amount > 0
                ? DamageResolver.Preview(target, [new DamageInstance(amount, request.DamageType, request.IsMagical)])
                : null;

            planned.Add(new TargetResolution(target, cover, save, false, amount, effectApplies, preview, notes));
        }

        return new SpellResolutionPlan(request, damageRoll, planned, concentration);
    }
}