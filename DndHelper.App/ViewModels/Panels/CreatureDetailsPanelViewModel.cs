using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DndHelper.App.Messages;
using DndHelper.Core.Models;
using Dock.Model.Mvvm.Controls;

namespace DndHelper.App.ViewModels.Panels;

/// <summary>Stat block for the creature currently selected in the browser. Safe to float onto a second monitor.</summary>
public partial class CreatureDetailsPanelViewModel : Tool
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCreature))]
    [NotifyPropertyChangedFor(nameof(EditLabel))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    private Creature? _creature;

    public bool HasCreature => Creature is not null;

    public string EditLabel => Creature?.SourceKey == ContentSources.Custom ? "Edit" : "Clone & Edit";

    public CreatureDetailsPanelViewModel()
    {
        Id = "CreatureDetails";
        Title = "Creature Details";
        CanClose = false;

        WeakReferenceMessenger.Default.Register<CreatureDetailsPanelViewModel, CreatureSelectedMessage>(
            this, static (r, m) => r.Creature = m.Creature);
    }

    [RelayCommand(CanExecute = nameof(HasCreature))]
    private void Edit()
    {
        if (Creature is { } creature)
            WeakReferenceMessenger.Default.Send(new EditCreatureMessage(creature));
    }
}