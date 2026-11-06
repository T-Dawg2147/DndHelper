using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DndHelper.App.Docking;
using DndHelper.Core;
using Dock.Model.Controls;

namespace DndHelper.App.ViewModels;

public partial class MainWindowViewModel: ObservableObject
{
    private readonly DockFactory _factory;

    [ObservableProperty] private IRootDock? _layout;

    public string Title => $"{AppInfo.Name} - {AppInfo.RulesEdition}";

    public MainWindowViewModel(DockFactory factory)
    {
        _factory = factory;
        BuildLayout();
    }

    [RelayCommand]
    private void ResetLayout()
    {
        if (Layout is { } old && old.Close.CanExecute(null))
            old.Close.Execute(null);
        
        BuildLayout();
    }

    private void BuildLayout()
    {
        var layout = _factory.CreateLayout();
        _factory.InitLayout(layout);
        Layout = layout;
    }
}
