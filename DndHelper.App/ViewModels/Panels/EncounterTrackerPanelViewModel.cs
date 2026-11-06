using Dock.Model.Mvvm.Controls;

namespace DndHelper.App.ViewModels.Panels;

public class EncounterTrackerPanelViewModel : Tool
{
    public EncounterTrackerPanelViewModel()
    {
        Id = "EncounterTracker";
        Title = "Encounter Tracker";
        CanClose = false;
    }
}