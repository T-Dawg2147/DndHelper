using DndHelper.Core.Rules;

namespace DndHelper.Core.Encounters;

/// <summary>Initiative, rounds and turns, with every rules action (damage, healing, conditions, death saves) logged.</summary>
public sealed class Encounter
{
    private readonly DiceRoller _roller;
    private readonly EffectProcessor _effects;
    private readonly ConcentrationManager _concentration;
    private readonly DeathSaveResolver _deathSaves;
    private readonly SpellResolver _spells;
    private readonly List<Participant> _turns = [];
    private int _index = -1;
    private int _sequence;

    public Encounter(IRandomSource random)
    {
        _roller = new DiceRoller(random);
        var saves = new SavingThrowResolver(_roller);
        _effects = new EffectProcessor(_roller, saves);
        _concentration = new ConcentrationManager(saves);
        _deathSaves = new DeathSaveResolver(_roller);
        _spells = new SpellResolver(_roller, saves, _concentration);
    }

    public event Action<LogEntry>? Logged;

    /// <summary>Increases every time anything is logged. Lets callers tell whether the encounter has changed since a moment ago.</summary>
    public int Revision { get; private set; }

    public Participant? Find(string? id) => id is null ? null : _turns.FirstOrDefault(t => t.Id == id);

    /// <summary>Participants in turn order (highest initiative first).</summary>
    public IReadOnlyList<Participant> Turns => _turns;

    public int Round { get; private set; }
    public bool IsStarted { get; private set; }
    public Participant? Current => IsStarted && _index >= 0 && _index < _turns.Count ? _turns[_index] : null;

    private IEnumerable<Combatant> Everyone => _turns.Select(t => t.Combatant);

    public Participant Add(Combatant combatant, Side side, int initiativeBonus = 0)
    {
        combatant.Name = UniqueName(combatant.Name);

        var participant = new Participant
        {
            Combatant = combatant,
            Side = side,
            InitiativeBonus = initiativeBonus,
            Sequence = _sequence++
        };

        if (IsStarted)
            participant.Initiative = _roller.RollD20(RollMode.Normal, initiativeBonus).Total;

        _turns.Add(participant);
        Resort();
        Log($"{participant.Name} joins the encounter.");
        return participant;
    }

    public void Remove(Participant participant)
    {
        var i = _turns.IndexOf(participant);
        if (i < 0) return;

        var wasCurrent = IsStarted && i == _index;
        _turns.RemoveAt(i);
        Log($"{participant.Name} leaves the encounter.");

        if (!IsStarted) return;

        if (_turns.Count == 0)
        {
            End();
            return;
        }

        if (i < _index)
        {
            _index--;
        }
        else if (wasCurrent)
        {
            _index--;
            Advance();
        }
    }

    public void RollInitiative()
    {
        foreach (var p in _turns)
            p.Initiative = _roller.RollD20(RollMode.Normal, p.InitiativeBonus).Total;

        Resort();
        Log("Initiative rolled: " + string.Join(", ", _turns.Select(t => $"{t.Name} {t.Initiative}")));
    }

    public void SetInitiative(Participant participant, int value)
    {
        participant.Initiative = value;
        Resort();
        Log($"{participant.Name}'s initiative set to {value}.");
    }

    public bool Start()
    {
        if (_turns.Count == 0 || IsStarted) return false;

        IsStarted = true;
        Round = 1;
        _index = -1;
        Log("The encounter begins.");
        Advance();
        return true;
    }

    public void NextTurn()
    {
        if (Current is not { } current) return;

        if (!current.Combatant.IsDead)
            LogLines(_effects.EndTurn(current.Combatant).Log);

        Advance();
    }

    public void End()
    {
        if (!IsStarted) return;

        Log($"The encounter ends after {Round} round{(Round == 1 ? "" : "s")}.");
        IsStarted = false;
        _index = -1;
        Round = 0;
    }

    public DamageResult ApplyDamage(Participant target, int amount, DamageType type, bool isMagical = true, bool isCritical = false)
    {
        var result = DamageResolver.Apply(target.Combatant, [new DamageInstance(amount, type, isMagical, isCritical)]);

        Log($"{Math.Max(0, amount)} {type} damage to {target.Name}{(isCritical ? " (critical)" : "")}.");
        LogLines(result.Steps);

        if (target.Combatant.ConcentratingOn is not null && result.AdjustedTotal > 0)
            Log(_concentration.CheckAfterDamage(target.Combatant, result.AdjustedTotal, Everyone).Message);

        return result;
    }

    public RecoveryResult Heal(Participant target, int amount)
    {
        var result = RecoveryResolver.Heal(target.Combatant, amount);
        Log(result.Message);
        return result;
    }

    public RecoveryResult GrantTemporaryHp(Participant target, int amount)
    {
        var result = RecoveryResolver.GrantTemporaryHp(target.Combatant, amount);
        Log(result.Message);
        return result;
    }

    public DeathSaveResult RollDeathSave(Participant target)
    {
        var result = _deathSaves.Roll(target.Combatant);
        Log(result.Message);
        return result;
    }

    /// <summary>Adds the condition if absent, removes it if present. Returns true when it is now applied.</summary>
    public bool ToggleCondition(Participant target, Condition condition)
    {
        var set = target.Combatant.Conditions;
        var applied = set.Add(condition);
        if (!applied) set.Remove(condition);

        Log(applied ? $"{target.Name} is now {condition}." : $"{target.Name} is no longer {condition}.");
        return applied;
    }

    /// <summary>Rolls damage and saves and works out each target's outcome. Nothing changes and nothing is logged until <see cref="CommitSpell"/>.</summary>
    public SpellResolutionPlan PlanSpell(SpellEffectRequest request, IEnumerable<Participant> targets)
        => _spells.Plan(request, targets.Select(t => t.Combatant));

    /// <summary>Applies a planned spell, logging every step. The returned result can be passed to <see cref="UndoSpell"/>.</summary>
    public CommittedResolution CommitSpell(SpellResolutionPlan plan)
    {
        var request = plan.Request;
        var caster = request.Caster?.Name ?? "Someone";
        var roll = plan.DamageRoll is { } damage ? $" (damage {damage})" : "";
        Log($"{caster} casts {request.Name}{roll}.");

        var committed = plan.Commit(Everyone);

        foreach (var target in plan.Targets)
        {
            if (target.Skipped)
            {
                Log($"{target.Target.Name}: {string.Join(" ", target.Notes)}");
                continue;
            }

            if (target.Save is { } save)
                Log($"{target.Target.Name}: {save}{(target.Notes.Count > 0 ? " " + string.Join(" ", target.Notes) : "")}");

            if (target.EffectApplies && request.Effect is { } effect)
                Log($"{target.Target.Name} is affected by {effect.Name}.");
        }

        foreach (var result in committed.Damage)
        {
            Log($"{result.Target.Name} takes {result.AdjustedTotal} {request.DamageType} damage from {request.Name}.");
            LogLines(result.Steps);
        }

        foreach (var check in committed.ConcentrationChecks)
            Log(check.Message);

        if (request.Caster?.ConcentratingOn is { } concentrating)
            Log($"{caster} is concentrating on {concentrating}.");

        return committed;
    }

    /// <summary>
    /// Puts everyone back as they were before the spell. Only safe straight after <see cref="CommitSpell"/>;
    /// compare <see cref="Revision"/> to decide.
    /// </summary>
    public void UndoSpell(CommittedResolution committed, string spellName)
    {
        committed.Undo();
        Log($"{spellName} was undone.");
    }

    private void Advance()
    {
        for (var i = 0; i < _turns.Count; i++)
        {
            _index++;
            if (_index >= _turns.Count)
            {
                _index = 0;
                Round++;
            }

            if (_turns[_index].Combatant.IsDead) continue;

            BeginTurn(_turns[_index]);
            return;
        }

        Log("Everyone is down; no one can take a turn.");
    }

    private void BeginTurn(Participant participant)
    {
        participant.ResetTurn();
        Log($"{participant.Name}'s turn.");
        LogLines(_effects.StartTurn(participant.Combatant).Log);

        if (participant.Combatant.IsDying)
            Log($"{participant.Name} is dying and needs a death saving throw.");
    }

    private string UniqueName(string name)
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? "Creature" : name.Trim();
        var taken = _turns.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(baseName)) return baseName;

        for (var n = 2; ; n++)
        {
            var candidate = $"{baseName} {n}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    private void Resort()
    {
        var current = Current;

        _turns.Sort((a, b) =>
        {
            var byInitiative = b.Initiative.CompareTo(a.Initiative);
            if (byInitiative != 0) return byInitiative;

            var byBonus = b.InitiativeBonus.CompareTo(a.InitiativeBonus);
            return byBonus != 0 ? byBonus : a.Sequence.CompareTo(b.Sequence);
        });

        if (current is not null)
            _index = _turns.IndexOf(current);
    }

    private void LogLines(IEnumerable<string> lines)
    {
        foreach (var line in lines) Log(line);
    }

    private void Log(string text)
    {
        Revision++;
        Logged?.Invoke(new LogEntry(Round, text));
    }
}
