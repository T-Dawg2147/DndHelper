using Dock.Model.Mvvm.Controls;

namespace DndHelper.App.ViewModels.Panels;

public class CreatureDetailsPanelViewModel : Tool
{
    public CreatureDetailsPanelViewModel()
    {
        Id = "CreatureDetails";
        Title = "Creature Details";
        CanClose = false;
    }
}