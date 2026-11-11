using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DndHelper.App.Messages;
using DndHelper.Core.Characters;
using DndHelper.Core.Editing;
using DndHelper.Core.Models;
using DndHelper.Core.Rules;
using DndHelper.Core.Services;
using Dock.Model.Mvvm.Controls;
using Microsoft.Extensions.Logging;

namespace DndHelper.App.ViewModels.Panels;

/// <summary>Create custom creatures, clone and tweak downloaded ones, or generate a random NPC.</summary>
public partial class CreatureEditorPanelViewModel : Document
{
    private const string RandomChoice = "Random";

    private readonly IContentStore _store;
    private readonly ILogger<CreatureEditorPanelViewModel> _logger;
    private readonly NpcGenerator _generator = new(new SystemRandomSource());

    [ObservableProperty] private CreatureDraft _draft = CreatureDraft.Blank();
    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private bool _hasErrors;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _selectedRole = RandomChoice;
    [ObservableProperty] private string _selectedSpecies = RandomChoice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteLabel))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private bool _isSaved;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteLabel))]
    private bool _deleteArmed;

    public string DeleteLabel => DeleteArmed ? "Click again to delete" : "Delete";

    public IReadOnlyList<string> RoleOptions { get; } = [RandomChoice, .. NpcRoles.Names];
    public IReadOnlyList<string> SpeciesOptions { get; } = [RandomChoice, .. SpeciesCatalog.Names];

    public CreatureEditorPanelViewModel(IContentStore store, ILogger<CreatureEditorPanelViewModel> logger)
    {
        _store = store;
        _logger = logger;

        Id = "CreatureEditor";
        Title = "Creature Editor";
        CanClose = false;

        WeakReferenceMessenger.Default.Register<CreatureEditorPanelViewModel, EditCreatureMessage>(
            this, static (r, m) => r.Open(m.Creature));
    }

    private void Open(Creature creature)
    {
        Show(CreatureDraft.FromCreature(creature), creature.SourceKey == ContentSources.Custom);
        StatusText = creature.SourceKey == ContentSources.Custom
            ? $"Editing {creature.Name}."
            : $"Editing a copy of {creature.Name}. Save to add it to your creatures.";
    }

    private void Show(CreatureDraft draft, bool saved)
    {
        Draft = draft;
        IsSaved = saved;
        DeleteArmed = false;
        HasErrors = false;
        ErrorText = "";
        StatusText = "";
    }

    [RelayCommand]
    private void New() => Show(CreatureDraft.Blank(), false);

    [RelayCommand]
    private void GenerateNpc()
    {
        var profile = _generator.Generate(
            SelectedRole == RandomChoice ? null : SelectedRole,
            SelectedSpecies == RandomChoice ? null : SelectedSpecies);

        Show(_generator.CreateDraft(profile), false);
        StatusText = $"Generated {profile.Name}, a {profile.Species} {profile.Role}. Tweak anything, then Save.";
    }

    [RelayCommand]
    private void AddTrait() => Draft.Traits.Add(new EntryDraft());

    [RelayCommand]
    private void AddAction() => Draft.Actions.Add(new EntryDraft());

    [RelayCommand]
    private void RemoveTrait(EntryDraft? entry)
    {
        if (entry is not null) Draft.Traits.Remove(entry);
    }

    [RelayCommand]
    private void RemoveAction(EntryDraft? entry)
    {
        if (entry is not null) Draft.Actions.Remove(entry);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        DeleteArmed = false;

        if (!Draft.TryBuild(out var creature, out var errors))
        {
            ErrorText = string.Join(Environment.NewLine, errors);
            HasErrors = true;
            StatusText = "";
            return;
        }

        HasErrors = false;
        ErrorText = "";

        try
        {
            await _store.SaveCustomCreatureAsync(creature!);
            IsSaved = true;
            StatusText = $"Saved {creature!.Name}.";

            WeakReferenceMessenger.Default.Send(new ContentUpdatedMessage());
            WeakReferenceMessenger.Default.Send(new CreatureSelectedMessage(creature));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save creature {Key}", Draft.Key);
            StatusText = $"Could not save: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(IsSaved))]
    private async Task DeleteAsync()
    {
        if (!DeleteArmed)
        {
            DeleteArmed = true;
            return;
        }

        DeleteArmed = false;

        try
        {
            await _store.DeleteCustomCreatureAsync(Draft.Key);
            var name = Draft.Name;
            Show(CreatureDraft.Blank(), false);
            StatusText = $"Deleted {name}.";
            WeakReferenceMessenger.Default.Send(new ContentUpdatedMessage());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete creature {Key}", Draft.Key);
            StatusText = $"Could not delete: {ex.Message}";
        }
    }
}