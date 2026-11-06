using Dock.Model.Mvvm.Controls;

namespace DndHelper.App.ViewModels.Panels;

public class BattleLogPanelViewModel : Tool
{
    public BattleLogPanelViewModel()
    {
        Id = "BattleLog";
        Title = "Battle Log";
        CanClose = false;
    }
}