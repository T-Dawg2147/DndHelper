namespace DndHelper.Core.Rules;

public sealed record TurnProcessingResult(
    IReadOnlyList<string> Log,
    IReadOnlyList<DamageResult> Damage,
    IReadOnlyList<ActiveEffect> Removed);

/// <summary>Handles effects at the start and end of a creature's turn: damage over time, repeat saves and durations.</summary>
public sealed class EffectProcessor(DiceRoller roller, SavingThrowResolver saves)
{
    public TurnProcessingResult StartTurn(Combatant creature) => Process(creature, TurnTiming.StartOfTurn, tickDuration: false);

    /// <summary>Also counts down durations; effects reaching 0 rounds are removed.</summary>
    public TurnProcessingResult EndTurn(Combatant creature) => Process(creature, TurnTiming.EndOfTurn, tickDuration: true);

    private TurnProcessingResult Process(Combatant creature, TurnTiming timing, bool tickDuration)
    {
        var log = new List<string>();
        var damage = new List<DamageResult>();
        var removed = new List<ActiveEffect>();

        foreach (var effect in creature.Effects.ToList())
        {
            if (effect.DamageOverTime is { } dot && dot.Timing == timing && !creature.IsDead)
            {
                var rolled = roller.Roll(dot.Damage);
                var result = DamageResolver.Apply(creature, [new DamageInstance(Math.Max(0, rolled.Total), dot.Type, dot.IsMagical)]);
                damage.Add(result);
                log.Add($"{effect.Name}: {rolled} {dot.Type} damage to {creature.Name}.");
                log.AddRange(result.Steps);
            }

            if (effect.RepeatSave is { } repeat && repeat.Timing == timing)
            {
                var save = saves.Resolve(creature, repeat.Ability, repeat.Dc);
                log.Add($"{effect.Name}: {save}");
                if (save.Success)
                {
                    creature.Effects.Remove(effect);
                    removed.Add(effect);
                    log.Add($"{effect.Name} ends on {creature.Name}.");
                    continue;
                }
            }

            if (tickDuration && effect.RemainingRounds is { } rounds)
            {
                effect.RemainingRounds = rounds - 1;
                if (effect.RemainingRounds <= 0)
                {
                    creature.Effects.Remove(effect);
                    removed.Add(effect);
                    log.Add($"{effect.Name} expires on {creature.Name}.");
                }
            }
        }

        return new TurnProcessingResult(log, damage, removed);
    }
}