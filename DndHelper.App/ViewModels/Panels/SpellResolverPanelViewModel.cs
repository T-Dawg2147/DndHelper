using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DndHelper.App.Messages;
using DndHelper.App.Services;
using DndHelper.Core.Encounters;
using DndHelper.Core.Models;
using DndHelper.Core.Rules;
using DndHelper.Maps.Areas;
using Dock.Model.Mvvm.Controls;

namespace DndHelper.App.ViewModels.Panels;

/// <summary>A creature inside the placed area, with the choices the DM can make for it.</summary>
public sealed partial class TargetRow : ObservableObject
{
    private static readonly IReadOnlyList<CoverType> Covers = Enum.GetValues<CoverType>();

    private readonly Action _changed;

    public TargetRow(Participant participant, AreaTarget target, bool include, CoverType cover, Action changed)
    {
        Participant = participant;
        Reached = target.Reached;
        _changed = changed;
        _include = include;
        _cover = cover;

        var c = participant.Combatant;
        Label = $"{participant.Name}  -  {target.DistanceFeet} ft.  -  HP {c.CurrentHp}/{c.MaxHp}"
                + (target.Reached ? "" : "  -  blocked by a wall");
    }

    public Participant Participant { get; }
    public bool Reached { get; }
    public string Label { get; }
    public IReadOnlyList<CoverType> CoverChoices => Covers;

    [ObservableProperty] private bool _include;
    [ObservableProperty] private CoverType _cover;

    partial void OnIncludeChanged(bool value) => _changed();
    partial void OnCoverChanged(CoverType value) => _changed();
}

public sealed record CasterOption(string? ParticipantId, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Sets up a spell or area effect, previews every target's save, damage and resistances, then commits the result
/// to the encounter (with undo and a battle log).
/// </summary>
public partial class SpellResolverPanelViewModel : Tool
{
    private static readonly HashSet<string> KeepPlan =
    [
        nameof(PreviewText), nameof(StatusText), nameof(HasPlan), nameof(CanUndo), nameof(TargetSummary)
    ];

    private readonly EncounterSession _session;
    private readonly AreaSession _area;

    private Spell? _lastSpell;
    private SpellResolutionPlan? _plan;
    private int _planRevision;
    private CommittedResolution? _committed;
    private string _committedName = "";
    private int _committedRevision;

    [ObservableProperty] private string _spellName = "Spell";
    [ObservableProperty] private string _damageText = "";
    [ObservableProperty] private DamageType _selectedDamageType = DamageType.Fire;
    [ObservableProperty] private string _selectedSave = "None";
    [ObservableProperty] private decimal? _saveDc = 15;
    [ObservableProperty] private bool _halfOnSave = true;
    [ObservableProperty] private bool _isMagical = true;
    [ObservableProperty] private bool _concentration;
    [ObservableProperty] private string _selectedEffectCondition = "None";
    [ObservableProperty] private decimal? _durationRounds = 10;
    [ObservableProperty] private CasterOption? _selectedCaster;

    [ObservableProperty] private AreaShapeKind _selectedKind = AreaShapeKind.Sphere;
    [ObservableProperty] private decimal? _sizeFeet = 20;
    [ObservableProperty] private decimal? _widthFeet = 5;

    [ObservableProperty] private string _targetSummary = "Pick the Area tool on the map and click to place the effect.";
    [ObservableProperty] private string _previewText = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _hasPlan;
    [ObservableProperty] private bool _canUndo;

    public ObservableCollection<TargetRow> Targets { get; } = [];
    public ObservableCollection<CasterOption> Casters { get; } = [];

    public IReadOnlyList<DamageType> DamageTypes { get; } = Enum.GetValues<DamageType>();
    public IReadOnlyList<AreaShapeKind> Kinds { get; } = Enum.GetValues<AreaShapeKind>();

    public IReadOnlyList<string> SaveChoices { get; } = ["None", .. Enum.GetNames<Ability>()];
    public IReadOnlyList<string> ConditionChoices { get; } = ["None", .. Enum.GetNames<Condition>()];

    public SpellResolverPanelViewModel(EncounterSession session, AreaSession area)
    {
        _session = session;
        _area = area;

        Id = "SpellResolver";
        Title = "Spell Resolver";
        CanClose = false;

        Casters.Add(new CasterOption(null, "(no caster)"));
        SelectedCaster = Casters[0];

        _session.Changed += OnSessionChanged;
        _area.TargetsChanged += RebuildTargets;

        WeakReferenceMessenger.Default.Register<SpellResolverPanelViewModel, SpellSelectedMessage>(
            this, static (r, m) => r._lastSpell = m.Spell);

        PushArea();
    }

    private Encounter Encounter => _session.Encounter;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is not null && !KeepPlan.Contains(e.PropertyName))
            ClearPlan();
    }

    partial void OnSelectedKindChanged(AreaShapeKind value) => PushArea();
    partial void OnSizeFeetChanged(decimal? value) => PushArea();
    partial void OnWidthFeetChanged(decimal? value) => PushArea();

    private void PushArea()
        => _area.Configure(SelectedKind, (int)Math.Round(SizeFeet ?? 0), (int)Math.Round(WidthFeet ?? 5));

    private void ClearPlan()
    {
        if (_plan is null) return;

        _plan = null;
        HasPlan = false;
        PreviewText = "The settings changed. Preview again before applying.";
    }

    private void OnSessionChanged()
    {
        var selectedId = SelectedCaster?.ParticipantId;

        Casters.Clear();
        Casters.Add(new CasterOption(null, "(no caster)"));
        foreach (var participant in Encounter.Turns)
            Casters.Add(new CasterOption(participant.Id, participant.Name));

        SelectedCaster = Casters.FirstOrDefault(c => c.ParticipantId == selectedId) ?? Casters[0];

        if (_plan is not null && Encounter.Revision != _planRevision)
            ClearPlan();

        CanUndo = _committed is not null && Encounter.Revision == _committedRevision;
    }

    private void RebuildTargets()
    {
        var previous = Targets.ToDictionary(t => t.Participant.Id);
        var unlinked = 0;

        Targets.Clear();
        foreach (var target in _area.Targets)
        {
            var participant = Encounter.Find(target.Token.ParticipantId);
            if (participant is null)
            {
                unlinked++;
                continue;
            }

            previous.TryGetValue(participant.Id, out var old);
            var keep = old is not null && old.Reached == target.Reached;

            Targets.Add(new TargetRow(
                participant, target,
                keep ? old!.Include : target.Reached,
                keep ? old!.Cover : CoverType.None,
                ClearPlan));
        }

        if (_area.Template is null)
        {
            TargetSummary = "Pick the Area tool on the map and click to place the effect.";
        }
        else
        {
            TargetSummary = $"{Targets.Count} creature{(Targets.Count == 1 ? "" : "s")} in the area."
                            + (unlinked > 0 ? $" {unlinked} token{(unlinked == 1 ? "" : "s")} ignored (not in the encounter; use 'Place encounter combatants' on the map)." : "");
        }

        ClearPlan();
    }

    [RelayCommand]
    private void LoadSpell()
    {
        if (_lastSpell is not { } spell)
        {
            StatusText = "Select a spell in the Spells list first.";
            return;
        }

        SpellName = spell.Name;
        DamageText = DiceExpression.TryParse(spell.DamageRoll, out _) ? spell.DamageRoll.Trim() : "";

        if (DamageTypeParser.FindAll(string.Join(" ", spell.DamageTypes)).FirstOrDefault() is { } type
            && spell.DamageTypes.Count > 0)
            SelectedDamageType = type;

        SelectedSave = AbilityExtensions.TryParse(spell.SavingThrowAbility, out var ability) ? ability.ToString() : "None";
        Concentration = spell.Concentration;
        HalfOnSave = spell.Description.Contains("half as much damage", StringComparison.OrdinalIgnoreCase);

        if (AreaTemplate.ParseKind(spell.ShapeType) is { } kind && spell.ShapeSize is { } size)
        {
            SelectedKind = kind;
            SizeFeet = (decimal)Math.Round(size);
        }

        StatusText = $"Loaded {spell.Name}. Check the save DC, any effect and the area, then preview.";
    }

    [RelayCommand]
    private void Preview()
    {
        DiceExpression? damage = null;
        if (!string.IsNullOrWhiteSpace(DamageText) && !DiceExpression.TryParse(DamageText, out damage))
        {
            StatusText = $"'{DamageText}' is not a valid dice expression (try 8d6 or 2d8+3).";
            return;
        }

        Ability? save = Enum.TryParse<Ability>(SelectedSave, out var parsedSave) ? parsedSave : null;

        ActiveEffect? effect = null;
        var hasCondition = Enum.TryParse<Condition>(SelectedEffectCondition, out var condition);
        if (hasCondition || Concentration)
        {
            effect = new ActiveEffect
            {
                Name = SpellName,
                RequiresConcentration = Concentration,
                RemainingRounds = DurationRounds is > 0 ? (int)Math.Round(DurationRounds.Value) : null
            };
            if (hasCondition) effect.GrantedConditions.Add(condition);
        }

        if (damage is null && save is null && effect is null)
        {
            StatusText = "Nothing to resolve: add damage, a saving throw or an effect.";
            return;
        }

        var included = Targets.Where(t => t.Include).ToList();
        if (included.Count == 0)
        {
            StatusText = "No targets are included. Place an area over some linked tokens first.";
            return;
        }

        var caster = Encounter.Find(SelectedCaster?.ParticipantId)?.Combatant;

        var request = new SpellEffectRequest
        {
            Name = string.IsNullOrWhiteSpace(SpellName) ? "Spell" : SpellName.Trim(),
            Caster = caster,
            Damage = damage,
            DamageType = SelectedDamageType,
            IsMagical = IsMagical,
            SaveAbility = save,
            SaveDc = (int)Math.Round(SaveDc ?? 10),
            HalfDamageOnSave = HalfOnSave,
            Effect = effect,
            CoverByTargetId = included.ToDictionary(t => t.Participant.Combatant.Id, t => t.Cover)
        };

        var plan = Encounter.PlanSpell(request, included.Select(t => t.Participant));
        PreviewText = Describe(plan);
        _plan = plan;
        _planRevision = Encounter.Revision;
        HasPlan = true;
        StatusText = "Preview rolled. Apply it to change the encounter, or preview again to re-roll.";
    }

    private static string Describe(SpellResolutionPlan plan)
    {
        var lines = new List<string>();
        if (plan.DamageRoll is { } roll)
            lines.Add($"Damage roll: {roll}");

        foreach (var t in plan.Targets)
        {
            var parts = new List<string>();

            if (t.Skipped)
            {
                parts.AddRange(t.Notes);
            }
            else
            {
                if (t.Save is { } save) parts.Add(save.ToString());

                if (t.DamageAmount > 0 && t.Preview is { } d)
                {
                    var text = $"{d.AdjustedTotal} {plan.Request.DamageType} damage";
                    if (d.AdjustedTotal != t.DamageAmount)
                        text += $" (from {t.DamageAmount} after resistances and vulnerabilities)";
                    text += $", HP {d.Before.CurrentHp} to {Math.Max(0, d.Before.CurrentHp - d.HpLost)}";
                    if (d.Died) text += ", dies";
                    else if (d.DroppedToZero) text += ", drops to 0 HP";
                    parts.Add(text);
                }
                else if (plan.Request.Damage is not null)
                {
                    parts.Add("no damage");
                }

                parts.AddRange(t.Notes);
                if (t.EffectApplies && plan.Request.Effect is { } effect) parts.Add($"gains {effect.Name}");
            }

            lines.Add($"{t.Target.Name}: {string.Join("; ", parts)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    [RelayCommand]
    private void Apply()
    {
        if (_plan is not { } plan)
        {
            StatusText = "Preview first.";
            return;
        }

        _committed = Encounter.CommitSpell(plan);
        _committedName = plan.Request.Name;
        _committedRevision = Encounter.Revision;
        _plan = null;
        HasPlan = false;
        PreviewText = "Applied. See the Battle Log for details.";
        StatusText = $"{_committedName} applied.";

        _session.NotifyChanged();
    }

    [RelayCommand]
    private void Undo()
    {
        if (_committed is not { } committed || Encounter.Revision != _committedRevision)
        {
            StatusText = "Undo is only available straight after applying a spell.";
            return;
        }

        Encounter.UndoSpell(committed, _committedName);
        _committed = null;
        PreviewText = "";
        StatusText = $"{_committedName} undone.";

        _session.NotifyChanged();
    }
}
