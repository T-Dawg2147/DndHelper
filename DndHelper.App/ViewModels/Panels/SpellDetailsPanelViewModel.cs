using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using DndHelper.App.Messages;
using DndHelper.Core.Models;
using Dock.Model.Mvvm.Controls;

namespace DndHelper.App.ViewModels.Panels;

public partial class SpellDetailsPanelViewModel : Tool
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSpell))] private Spell? _spell;

    public bool HasSpell => Spell is not null;

    public SpellDetailsPanelViewModel()
    {
        Id = "SpellDetails";
        Title = "Spell Details";
        CanClose = false;
        
        WeakReferenceMessenger.Default.Register<SpellDetailsPanelViewModel, SpellSelectedMessage>(
            this, static (r, m) => r.Spell = m.Spell);
    }
}