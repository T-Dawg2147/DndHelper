using Avalonia.Controls;
using Avalonia.Input;

namespace DndHelper.App.View;

/// <summary>
/// A read-only copy of the map for the players' screen. It shows tokens and area effects but no DM tools,
/// movement overlays or enemy health bars. Drag it to the second monitor and press F11.
/// </summary>
public partial class PlayerViewWindow : Window
{
    public PlayerViewWindow() => InitializeComponent();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.F11)
        {
            WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && WindowState == WindowState.FullScreen)
        {
            WindowState = WindowState.Normal;
            e.Handled = true;
        }
    }
}
