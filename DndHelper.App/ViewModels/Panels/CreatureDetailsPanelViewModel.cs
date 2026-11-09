using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using DndHelper.App.Messages;
using DndHelper.Core.Models;
using Dock.Model.Mvvm.Controls;

namespace DndHelper.App.ViewModels.Panels;

public partial class CreatureDetailsPanelViewModel : Tool
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasCreature))]
    private Creature? _creature;

    public bool HasCreature => Creature is not null;
    
    public CreatureDetailsPanelViewModel()
    {
        Id = "CreatureDetails";
        Title = "Creature Details";
        CanClose = false;
        
        WeakReferenceMessenger.Default.Register<CreatureDetailsPanelViewModel, CreatureSelectedMessaged>(
            this, static (r, msg) => r.Creature = msg.Creature);
    }
}