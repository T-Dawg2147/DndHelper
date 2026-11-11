using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DndHelper.App.Messages;
using DndHelper.Core.Characters;
using DndHelper.Core.Models;
using DndHelper.Core.Rules;
using DndHelper.Core.Services;
using Dock.Model.Mvvm.Controls;
using Microsoft.Extensions.Logging;

namespace DndHelper.App.ViewModels.Panels;

public sealed record MethodOption(string Label, ScoreMethod Method);

public partial class SkillChoice(Skill skill) : ObservableObject
{
    public Skill Skill { get; } = skill;
    public string Name { get; } = skill.DisplayName();

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Create or edit a player character. Every change recalculates the sheet and re-validates.</summary>
public partial class CharacterBuilderPanelViewModel : Document
{
    private readonly ICharacterStore _store;
    private readonly ILogger<CharacterBuilderPanelViewModel> _logger;
    private readonly DiceRoller _roller = new(new SystemRandomSource());

    private string _characterId = Guid.NewGuid().ToString("N");
    private bool _loading;

    [ObservableProperty] private string _characterName = "";
    [ObservableProperty] private string? _selectedSpecies;
    [ObservableProperty] private string? _selectedClass;
    [ObservableProperty] private string? _selectedBackground;
    [ObservableProperty] private decimal? _characterLevel = 1;
    [ObservableProperty] private MethodOption _selectedMethod;

    [ObservableProperty] private decimal? _strength = 15;
    [ObservableProperty] private decimal? _dexterity = 14;
    [ObservableProperty] private decimal? _constitution = 13;
    [ObservableProperty] private decimal? _intelligence = 12;
    [ObservableProperty] private decimal? _wisdom = 10;
    [ObservableProperty] private decimal? _charisma = 8;

    [ObservableProperty] private bool _useSplitBonus = true;
    [ObservableProperty] private Ability? _selectedPlus2;
    [ObservableProperty] private Ability? _selectedPlus1;

    [ObservableProperty] private decimal? _manualArmorClass;
    [ObservableProperty] private string _characterNotes = "";

    [ObservableProperty] private IReadOnlyList<LabelValue> _previewLines = [];
    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private bool _hasErrors;
    [ObservableProperty] private string _skillHint = "";
    [ObservableProperty] private string _statusText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _canSave;

    public IReadOnlyList<string> SpeciesNames { get; } = SpeciesCatalog.Names;
    public IReadOnlyList<string> ClassNames { get; } = ClassCatalog.Names;
    public IReadOnlyList<string> BackgroundNames { get; } = BackgroundCatalog.Names;

    public IReadOnlyList<MethodOption> Methods { get; } =
    [
        new("Standard Array", ScoreMethod.StandardArray),
        new("Point Buy", ScoreMethod.PointBuy),
        new("Roll 4d6 (drop lowest)", ScoreMethod.Roll),
        new("Manual", ScoreMethod.Manual)
    ];

    public ObservableCollection<Ability> BonusAbilities { get; } = [];
    public ObservableCollection<SkillChoice> SkillChoices { get; } = [];

    public CharacterBuilderPanelViewModel(ICharacterStore store, ILogger<CharacterBuilderPanelViewModel> logger)
    {
        _store = store;
        _logger = logger;
        _selectedMethod = Methods[0];

        Id = "CharacterBuilder";
        Title = "Character Builder";
        CanClose = false;

        WeakReferenceMessenger.Default.Register<CharacterBuilderPanelViewModel, CharacterSelectedMessage>(
            this, static (r, m) => r.Load(m.Character));
        WeakReferenceMessenger.Default.Register<CharacterBuilderPanelViewModel, NewCharacterMessage>(
            this, static (r, msg) => r.Load(NewDefault()));

        Load(NewDefault());
    }

    partial void OnCharacterNameChanged(string value) => Recalculate();
    partial void OnSelectedSpeciesChanged(string? value) => Recalculate();
    partial void OnCharacterLevelChanged(decimal? value) => Recalculate();
    partial void OnSelectedMethodChanged(MethodOption value) => Recalculate();
    partial void OnStrengthChanged(decimal? value) => Recalculate();
    partial void OnDexterityChanged(decimal? value) => Recalculate();
    partial void OnConstitutionChanged(decimal? value) => Recalculate();
    partial void OnIntelligenceChanged(decimal? value) => Recalculate();
    partial void OnWisdomChanged(decimal? value) => Recalculate();
    partial void OnCharismaChanged(decimal? value) => Recalculate();
    partial void OnUseSplitBonusChanged(bool value) => Recalculate();
    partial void OnSelectedPlus2Changed(Ability? value) => Recalculate();
    partial void OnSelectedPlus1Changed(Ability? value) => Recalculate();
    partial void OnManualArmorClassChanged(decimal? value) => Recalculate();

    partial void OnSelectedClassChanged(string? value)
    {
        if (_loading) return;
        RebuildSkillChoices(null);
        Recalculate();
    }

    partial void OnSelectedBackgroundChanged(string? value)
    {
        if (_loading) return;
        RebuildBackgroundOptions();
        RebuildSkillChoices(null);
        Recalculate();
    }

    [RelayCommand]
    private void GenerateScores()
    {
        IReadOnlyList<int>? scores = SelectedMethod.Method switch
        {
            ScoreMethod.StandardArray => AbilityScoreGenerator.StandardArray,
            ScoreMethod.PointBuy => [8, 8, 8, 8, 8, 8],
            ScoreMethod.Roll => AbilityScoreGenerator.RollScores(_roller),
            _ => null
        };

        if (scores is null) return;

        _loading = true;
        Strength = scores[0];
        Dexterity = scores[1];
        Constitution = scores[2];
        Intelligence = scores[3];
        Wisdom = scores[4];
        Charisma = scores[5];
        _loading = false;
        Recalculate();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var character = BuildCharacter();
        if (CharacterValidator.Validate(character).Count > 0) return;

        try
        {
            await _store.SaveAsync(character);
            StatusText = $"Saved {character.Name}.";
            WeakReferenceMessenger.Default.Send(new CharactersChangedMessage(character.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save character {Id}", character.Id);
            StatusText = $"Could not save: {ex.Message}";
        }
    }

    [RelayCommand]
    private void New() => Load(NewDefault());

    private static PlayerCharacter NewDefault()
    {
        var background = BackgroundCatalog.All[0];
        var scores = AbilityScoreGenerator.StandardArray;

        return new PlayerCharacter
        {
            Species = SpeciesCatalog.All[0].Name,
            ClassName = ClassCatalog.All[0].Name,
            Background = background.Name,
            Method = ScoreMethod.StandardArray,
            BaseScores = AbilityExtensions.All.Select((a, i) => (a, score: scores[i])).ToDictionary(x => x.a, x => x.score),
            BackgroundBonuses = new Dictionary<Ability, int>
            {
                [background.AbilityOptions[0]] = 2,
                [background.AbilityOptions[1]] = 1
            }
        };
    }

    private void Load(PlayerCharacter character)
    {
        _loading = true;

        _characterId = character.Id;
        CharacterName = character.Name;
        SelectedSpecies = character.Species;
        SelectedClass = character.ClassName;
        SelectedBackground = character.Background;
        CharacterLevel = character.Level;
        SelectedMethod = Methods.First(m => m.Method == character.Method);

        Strength = character.BaseScores.GetValueOrDefault(Ability.Strength, 10);
        Dexterity = character.BaseScores.GetValueOrDefault(Ability.Dexterity, 10);
        Constitution = character.BaseScores.GetValueOrDefault(Ability.Constitution, 10);
        Intelligence = character.BaseScores.GetValueOrDefault(Ability.Intelligence, 10);
        Wisdom = character.BaseScores.GetValueOrDefault(Ability.Wisdom, 10);
        Charisma = character.BaseScores.GetValueOrDefault(Ability.Charisma, 10);

        RebuildBackgroundOptions();
        UseSplitBonus = character.BackgroundBonuses.Count != 3;
        SelectedPlus2 = character.BackgroundBonuses.Where(b => b.Value == 2).Select(b => (Ability?)b.Key).FirstOrDefault()
                        ?? SelectedPlus2;
        SelectedPlus1 = character.BackgroundBonuses.Where(b => b.Value == 1).Select(b => (Ability?)b.Key).FirstOrDefault()
                        ?? SelectedPlus1;

        ManualArmorClass = character.ArmorClassOverride;
        CharacterNotes = character.Notes;
        RebuildSkillChoices(character.ClassSkills);

        StatusText = "";
        _loading = false;
        Recalculate();
    }

    private void RebuildBackgroundOptions()
    {
        BonusAbilities.Clear();
        if (BackgroundCatalog.Find(SelectedBackground) is not { } background)
        {
            SelectedPlus2 = null;
            SelectedPlus1 = null;
            return;
        }

        foreach (var ability in background.AbilityOptions) BonusAbilities.Add(ability);
        SelectedPlus2 = background.AbilityOptions[0];
        SelectedPlus1 = background.AbilityOptions[1];
    }

    private void RebuildSkillChoices(IEnumerable<Skill>? selected)
    {
        SkillChoices.Clear();

        var cls = ClassCatalog.Find(SelectedClass);
        if (cls is null) return;

        var granted = BackgroundCatalog.Find(SelectedBackground)?.Skills ?? [];
        var chosen = selected?.ToHashSet() ?? [];

        foreach (var skill in cls.SkillOptions.Where(s => !granted.Contains(s)))
        {
            var choice = new SkillChoice(skill) { IsSelected = chosen.Contains(skill) };
            choice.PropertyChanged += (_, _) => Recalculate();
            SkillChoices.Add(choice);
        }
    }

    private PlayerCharacter BuildCharacter()
    {
        var bonuses = new Dictionary<Ability, int>();
        if (BackgroundCatalog.Find(SelectedBackground) is { } background)
        {
            if (UseSplitBonus)
            {
                if (SelectedPlus2 is { } plus2) bonuses[plus2] = 2;
                if (SelectedPlus1 is { } plus1 && plus1 != SelectedPlus2) bonuses[plus1] = 1;
            }
            else
            {
                foreach (var ability in background.AbilityOptions) bonuses[ability] = 1;
            }
        }

        return new PlayerCharacter
        {
            Id = _characterId,
            Name = CharacterName.Trim(),
            Species = SelectedSpecies ?? "",
            ClassName = SelectedClass ?? "",
            Background = SelectedBackground ?? "",
            Level = ToInt(CharacterLevel),
            Method = SelectedMethod.Method,
            BaseScores = new Dictionary<Ability, int>
            {
                [Ability.Strength] = ToInt(Strength),
                [Ability.Dexterity] = ToInt(Dexterity),
                [Ability.Constitution] = ToInt(Constitution),
                [Ability.Intelligence] = ToInt(Intelligence),
                [Ability.Wisdom] = ToInt(Wisdom),
                [Ability.Charisma] = ToInt(Charisma)
            },
            BackgroundBonuses = bonuses,
            ClassSkills = SkillChoices.Where(c => c.IsSelected).Select(c => c.Skill).ToList(),
            ArmorClassOverride = ManualArmorClass is { } ac ? ToInt(ac) : null,
            Notes = CharacterNotes
        };
    }

    private void Recalculate()
    {
        if (_loading) return;

        var character = BuildCharacter();
        var errors = CharacterValidator.Validate(character);

        ErrorText = string.Join(Environment.NewLine, errors);
        HasErrors = errors.Count > 0;
        CanSave = errors.Count == 0;
        SkillHint = BuildSkillHint(character);

        try
        {
            PreviewLines = CharacterCalculator.Compute(character).Properties;
        }
        catch (ArgumentException)
        {
            PreviewLines = [];
        }
    }

    private static string BuildSkillHint(PlayerCharacter character)
    {
        var cls = ClassCatalog.Find(character.ClassName);
        if (cls is null) return "";

        var hint = $"Choose {cls.SkillChoiceCount} class skills ({character.ClassSkills.Count} chosen).";
        if (BackgroundCatalog.Find(character.Background) is { } background)
            hint += $" {background.Name} already grants {string.Join(" and ", background.Skills.Select(s => s.DisplayName()))}.";
        return hint;
    }

    private static int ToInt(decimal? value) => (int)Math.Round(value ?? 0);
}