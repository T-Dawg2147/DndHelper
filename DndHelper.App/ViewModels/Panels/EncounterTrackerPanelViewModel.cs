using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DndHelper.App.Messages;
using DndHelper.App.Services;
using DndHelper.Core.Characters;
using DndHelper.Core.Encounters;
using DndHelper.Core.Models;
using DndHelper.Core.Rules;
using Dock.Model.Mvvm.Controls;

namespace DndHelper.App.ViewModels.Panels;

/// <summary>One line in the initiative list.</summary>
public sealed class ParticipantRow(Participant participant, bool isCurrent)
{
    private readonly Combatant _c = participant.Combatant;

    public Participant Participant { get; } = participant;
    public string Name => (isCurrent ? "▶ " : "") + Participant.Name;
    public string Initiative => $"Init {Participant.Initiative}";
    public string Side => Participant.Side.ToString();

    public string Stats
    {
        get
        {
            var temp = _c.TempHp > 0 ? $" (+{_c.TempHp} temp)" : "";
            return $"AC {_c.ArmorClass}   HP {_c.CurrentHp}/{_c.MaxHp}{temp}";
        }
    }

    public string Status
    {
        get
        {
            if (_c.IsDead) return "DEAD";
            if (_c.IsDying) return $"Dying: {_c.DeathSaveSuccesses} successes, {_c.DeathSaveFailures} failures";
            if (_c.IsPlayerCharacter && _c.CurrentHp == 0) return "Stable at 0 HP";

            var parts = _c.ActiveConditions.Select(c => c.ToString()).ToList();
            parts.AddRange(_c.Effects.Select(e => e.Name));
            if (_c.ConcentratingOn is { } spell) parts.Add($"Concentrating: {spell}");
            return string.Join(", ", parts);
        }
    }

    public string Economy
        => $"Action {Mark(!Participant.ActionUsed)}  Bonus {Mark(!Participant.BonusActionUsed)}  Reaction {Mark(!Participant.ReactionUsed)}";

    private static string Mark(bool available) => available ? "✓" : "✗";
}

/// <summary>Initiative order and turn tracking, linked to the M2 rules engine.</summary>
public partial class EncounterTrackerPanelViewModel : Tool
{
    private readonly EncounterSession _session;
    private Encounter _encounter => _session.Encounter;
    private Creature? _lastCreature;
    private PlayerCharacter? _lastCharacter;

    [ObservableProperty] private string _headerText = "Encounter not started";
    [ObservableProperty] private string _statusText = "Select a creature or character, then add it.";
    [ObservableProperty] private ParticipantRow? _selectedRow;
    [ObservableProperty] private decimal? _amount = 5;
    [ObservableProperty] private DamageType _selectedDamageType = DamageType.Slashing;
    [ObservableProperty] private bool _isMagical;
    [ObservableProperty] private bool _isCritical;
    [ObservableProperty] private Condition _selectedCondition = Condition.Prone;
    [ObservableProperty] private decimal? _initiativeInput = 10;

    public ObservableCollection<ParticipantRow> Rows { get; } = [];
    public IReadOnlyList<DamageType> DamageTypes { get; } = Enum.GetValues<DamageType>();
    public IReadOnlyList<Condition> Conditions { get; } = Enum.GetValues<Condition>();

    public EncounterTrackerPanelViewModel(EncounterSession session)
    {
        _session = session;

        Id = "EncounterTracker";
        Title = "Encounter Tracker";
        CanClose = false;

        _session.Changed += Rebuild;

        WeakReferenceMessenger.Default.Register<EncounterTrackerPanelViewModel, CreatureSelectedMessage>(
            this, static (r, m) => r._lastCreature = m.Creature);
        WeakReferenceMessenger.Default.Register<EncounterTrackerPanelViewModel, CharacterSelectedMessage>(
            this, static (r, m) => r._lastCharacter = m.Character);
    }

    private int Whole => (int)Math.Round(Amount ?? 0);

    /// <summary>Tells every panel the encounter changed; this panel rebuilds itself in <see cref="Rebuild"/>.</summary>
    private void Refresh() => _session.NotifyChanged();

    private void Rebuild()
    {
        var selectedId = SelectedRow?.Participant.Id;
        var current = _encounter.Current;

        Rows.Clear();
        foreach (var p in _encounter.Turns)
            Rows.Add(new ParticipantRow(p, p == current));

        SelectedRow = Rows.FirstOrDefault(r => r.Participant.Id == selectedId);

        HeaderText = _encounter.IsStarted
            ? $"Round {_encounter.Round}" + (current is null ? "" : $" - {current.Name}'s turn")
            : "Encounter not started";
    }

    private bool NeedTarget([System.Runtime.CompilerServices.CallerMemberName] string? _ = null)
    {
        if (SelectedRow is not null) return false;
        StatusText = "Select a combatant in the list first.";
        return true;
    }

    [RelayCommand]
    private void AddCreature()
    {
        if (_lastCreature is not { } creature)
        {
            StatusText = "Select a creature in the Creatures list first.";
            return;
        }

        _encounter.Add(CombatantFactory.FromCreature(creature), Side.Enemy, creature.InitiativeBonus);
        StatusText = $"Added {creature.Name}.";
        Refresh();
    }

    [RelayCommand]
    private void AddCharacter()
    {
        if (_lastCharacter is not { } character)
        {
            StatusText = "Select a character in the Characters list first.";
            return;
        }

        try
        {
            var sheet = CharacterCalculator.Compute(character);
            _encounter.Add(CharacterCombatantFactory.ToCombatant(character), Side.Party, sheet.Initiative);
            StatusText = $"Added {character.Name}.";
        }
        catch (ArgumentException)
        {
            StatusText = "That character is incomplete; finish it in the Character Builder first.";
        }

        Refresh();
    }

    [RelayCommand]
    private void RollInitiative()
    {
        _encounter.RollInitiative();
        Refresh();
    }

    [RelayCommand]
    private void SetInitiative()
    {
        if (NeedTarget()) return;
        _encounter.SetInitiative(SelectedRow!.Participant, (int)Math.Round(InitiativeInput ?? 0));
        Refresh();
    }

    [RelayCommand]
    private void Start()
    {
        if (!_encounter.Start())
            StatusText = _encounter.IsStarted ? "The encounter is already running." : "Add combatants first.";
        Refresh();
    }

    [RelayCommand]
    private void NextTurn()
    {
        _encounter.NextTurn();
        Refresh();
    }

    [RelayCommand]
    private void EndEncounter()
    {
        _encounter.End();
        Refresh();
    }

    [RelayCommand]
    private void Damage()
    {
        if (NeedTarget()) return;
        _encounter.ApplyDamage(SelectedRow!.Participant, Whole, SelectedDamageType, IsMagical, IsCritical);
        Refresh();
    }

    [RelayCommand]
    private void Heal()
    {
        if (NeedTarget()) return;
        _encounter.Heal(SelectedRow!.Participant, Whole);
        Refresh();
    }

    [RelayCommand]
    private void TempHp()
    {
        if (NeedTarget()) return;
        _encounter.GrantTemporaryHp(SelectedRow!.Participant, Whole);
        Refresh();
    }

    [RelayCommand]
    private void DeathSave()
    {
        if (NeedTarget()) return;
        _encounter.RollDeathSave(SelectedRow!.Participant);
        Refresh();
    }

    [RelayCommand]
    private void ToggleCondition()
    {
        if (NeedTarget()) return;
        _encounter.ToggleCondition(SelectedRow!.Participant, SelectedCondition);
        Refresh();
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (NeedTarget()) return;
        _encounter.Remove(SelectedRow!.Participant);
        Refresh();
    }

    [RelayCommand]
    private void ToggleAction() => ToggleEconomy(p => p.ActionUsed = !p.ActionUsed);

    [RelayCommand]
    private void ToggleBonus() => ToggleEconomy(p => p.BonusActionUsed = !p.BonusActionUsed);

    [RelayCommand]
    private void ToggleReaction() => ToggleEconomy(p => p.ReactionUsed = !p.ReactionUsed);

    private void ToggleEconomy(Action<Participant> toggle)
    {
        if (NeedTarget()) return;
        toggle(SelectedRow!.Participant);
        Refresh();
    }
}
