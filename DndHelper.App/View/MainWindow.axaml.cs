using Avalonia.Controls;
using Avalonia.Interactivity;
using DndHelper.App.ViewModels.Panels;
using Microsoft.Extensions.DependencyInjection;

namespace DndHelper.App.View;

public partial class MainWindow : Window
{
    private PlayerViewWindow? _player;

    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => _player?.Close();
    }

    private void OpenPlayerView(object? sender, RoutedEventArgs e)
    {
        if (_player is not null)
        {
            _player.Activate();
            return;
        }

        _player = new PlayerViewWindow { DataContext = App.Services.GetRequiredService<MapPanelViewModel>() };
        _player.Closed += (_, _) => _player = null;
        _player.Show();
    }
}
