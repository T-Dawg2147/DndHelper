using Dock.Model.Mvvm.Controls;

namespace DndHelper.App.ViewModels.Panels;

public class MapPanelViewModel : Document
{
    public MapPanelViewModel()
    {
        Id = "Map";
        Title = "Map";
        CanClose = false;
    }
}